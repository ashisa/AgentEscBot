using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using System.Net.Http;
using Newtonsoft.Json;
using System.Diagnostics;

namespace TestBot01
{
    [Serializable]
    public class Escalation : IDialog<object>
    {
        [NonSerialized]
        public static Dictionary<Activity, Activity> escRegister;
        [NonSerialized]
        public static List<Activity> agentRegister;
        [NonSerialized]
        public static List<Activity> userRegister;

        [NonSerialized]
        public Activity currActivity;

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceived);
        }

        private async Task MessageReceived(IDialogContext context, IAwaitable<object> result)
        {
            //Initializing escalation register
            if (escRegister == null) escRegister = new Dictionary<Activity, Activity>();
            if (agentRegister == null) agentRegister = new List<Activity>();
            if (userRegister == null) userRegister = new List<Activity>();

            var message = await result;

            var activity = message as Activity;
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            activity.Type = "typing";
            await connector.Conversations.SendToConversationAsync(activity);

            //Check if we are dealing with any escalation pairs
            //is it coming from an agent?
            bool agentMessage = false, userMessage = false, agentControlMessage = false;
            var agent = escRegister.Where(x => x.Key.From.Id == activity.From.Id);
            if (agent.Count() > 0) agentMessage = true;

            //Also check if this agent is in the roster
            var rosterCheck = agentRegister.Where(x => x.From.Id == activity.From.Id);
            if (rosterCheck.Count() > 0) agentMessage = true;

            //is it coming from a user?
            var user = escRegister.Where(x => x.Value.From.Id == activity.From.Id);
            if (user.Count() > 0) userMessage = true;

            if (agentMessage)
            {
                if (activity.Text == "IssueResolved" || activity.Text == "LogMeOut") agentControlMessage = true;

                Activity agentActivity = agent.ElementAt(0).Key;
                Activity userActivity = agent.ElementAt(0).Value;

                if (!agentControlMessage) //send this to user
                {
                    //Send this message to the user
                    ConnectorClient userConnector = new ConnectorClient(new Uri(userActivity.ServiceUrl));
                    Activity agentReply = userActivity.CreateReply($"{agentActivity.From.Name}: {activity.Text}");
                    await userConnector.Conversations.ReplyToActivityAsync(agentReply);
                }
                else
                {
                    await agentControlFlow(activity, userActivity);
                }
            }

            if (userMessage) //send this to the agent mapped to this user
            {
                activity.Type = "typing";
                await connector.Conversations.SendToConversationAsync(activity);

                Activity agentActivity = user.ElementAt(0).Key;
                Activity userActivity = user.ElementAt(0).Value;

                Debug.WriteLine($"this needs to go to {agentActivity.From.Name}");

                await DirectMessage(userActivity, agentActivity, activity.Text, false);
            }

            //Send it to LUIS if this message isn't happening between a user and an agent
            if (!agentMessage && !userMessage) //send it to LUIS
            {
                using (HttpClient client = new HttpClient())
                {
                    string uri = "https://westus.api.cognitive.microsoft.com/luis/v2.0/apps/<***luis-app-id ***>?subscription-key=<***luis api key***>&verbose=true&q=" + activity.Text;
                    HttpResponseMessage msg = await client.GetAsync(uri);

                    if (msg.IsSuccessStatusCode)
                    {
                        var jsonResponse = await msg.Content.ReadAsStringAsync();
                        var _Data = JsonConvert.DeserializeObject<LUISResponse>(jsonResponse);

                        string entityFound = "";
                        string topIntent = "";

                        if (_Data.entities.Count() != 0) entityFound = _Data.entities[0].entity;
                        if (_Data.intents.Count() != 0) topIntent = _Data.intents[0].intent;

                        await LuisProcess(activity, _Data.topScoringIntent.intent);
                    }
                }
            }
            context.Wait(MessageReceived);
        }

        private async Task agentControlFlow(Activity agentActivity, Activity userActivity)
        {
            switch (agentActivity.Text)
            {
                case "IssueResolved":
                    var agent = escRegister.Where(x => x.Key.From.Id == agentActivity.From.Id);
                    //Remove the agent and user map from the escalation register
                    escRegister.Remove(agent.ElementAt(0).Key);

                    //Add the agent back to the agent register
                    agentRegister.Add(agentActivity);
                    await DirectMessage(agentActivity.CreateReply(), agentActivity, $"You have been added to the roster", false);
                    await DirectMessage(userActivity.CreateReply(), userActivity, $"{agentActivity.From.Name} indicated that your problem resolved. Please let us know if that isn't correct.", false);

                    //Check the user register and create a new map
                    if (userRegister.Count > 0)
                    {
                        userActivity = userRegister.ElementAt(0);
                        await EscalateNext(userActivity, agentActivity);
                    }
                    break;

                case "LogMeOut":
                    if (escRegister.Count > 0)
                    {
                        var currEscalation = escRegister.Where(x => x.Key.From.Id == agentActivity.From.Id);
                        if (currEscalation.Count() > 0)
                        {
                            await DirectMessage(agentActivity.CreateReply($""), agentActivity, $"Please close any pending escalation before logging out.", false);
                        }
                    }
                    else
                    {
                        if (agentRegister.Count > 0)
                        {
                            var agentEntry = agentRegister.Where(x => x.From.Id == agentActivity.From.Id);
                            if (agentEntry.Count() > 0) agentRegister.Remove(agentEntry.ElementAt(0));
                        }
                        await DirectMessage(agentActivity.CreateReply($""), agentActivity, $"You have been logged out.", false);
                    }
                    break;
            }
        }

        private async Task LuisProcess(Activity activity, string intent)
        {
            switch (intent)
            {
                case "":
                case "None":
                    ConnectorClient connector01 = new ConnectorClient(new Uri(activity.ServiceUrl));
                    await connector01.Conversations.ReplyToActivityAsync(activity.CreateReply($"Sorry - I didn't understand that. Consider rephrasing please..."));
                    break;

                case "Escalation":
                    //Escalation was triggered
                    Activity userActivity = activity;
                    Activity agentActivity = null;

                    //Any agent available
                    if (agentRegister.Count != 0)
                    {
                        agentActivity = agentRegister.ElementAt(0);
                        await EscalateNext(userActivity, agentActivity);
                    }
                    else
                    {
                        //Add this user to the register to be paired with an agent later
                        userRegister.Add(activity);
                        Activity reply = activity.CreateReply($"I am sorry I couldn't help you. One of our agents will get in touch with you shortly.");
                        ConnectorClient connector03 = new ConnectorClient(new Uri(activity.ServiceUrl));
                        await connector03.Conversations.ReplyToActivityAsync(reply);
                    }
                    break;

                case "RegisterAgent":
                    //This person claims to be a human agent, add him to the agent register
                    agentRegister.Add(activity);
                    Activity agentReply = activity.CreateReply($"You have been added to the agent roster. You will be paired with users when they ask for escalation.");
                    ConnectorClient agentConnector = new ConnectorClient(new Uri(activity.ServiceUrl));
                    await agentConnector.Conversations.ReplyToActivityAsync(agentReply);
                    await agentConnector.Conversations.ReplyToActivityAsync(activity.CreateReply($"Please send LogMeOut when your shift is over."));

                    //Check if we have any pending escalations
                    if (userRegister.Count() > 0)
                    {
                        await EscalateNext(userRegister.ElementAt(0), activity);
                    }

                    break;

                default:
                    break;
            }
        }

        private async Task EscalateNext(Activity userActivity, Activity agentActivity) //user and agent activity supplied already
        {
            //Add them to escalation register
            escRegister.Add(agentActivity, userActivity);
            if (agentRegister.Contains(agentActivity)) agentRegister.Remove(agentActivity);
            if (userRegister.Contains(userActivity)) userRegister.Remove(userActivity);

            //Send a message to the agent about assignment
            await DirectMessage(userActivity.CreateReply(), agentActivity, $"You are now connected to {userActivity.From.Name}", true);
            await DirectMessage(userActivity, agentActivity, userActivity.Text, false);

            //Send an update to the user
            ConnectorClient userConnector = new ConnectorClient(new Uri(userActivity.ServiceUrl));
            //Activity agentReply = userActivity.CreateReply($"{agentActivity.From.Name}: {activity.Text}");
            await userConnector.Conversations.ReplyToActivityAsync(userActivity.CreateReply($"You are now connected to {agentActivity.From.Name} for a quick resolution."));
            await userConnector.Conversations.ReplyToActivityAsync(userActivity.CreateReply($"Any messages that you send now will be relayed to {agentActivity.From.Name} directly"));
        }

        private async Task DirectMessage(Activity userActivity, Activity agentActivity, string messageString, bool botMessage)
        {
            ConnectorClient directClient = new ConnectorClient(new Uri(agentActivity.ServiceUrl));
            Activity directReply = agentActivity.CreateReply($"{userActivity.From.Name}: \"{messageString}\"");
            await directClient.Conversations.ReplyToActivityAsync(directReply);

            if (botMessage)
            {
                directReply.Text = $"Please send IssueResolved after the resolution.";
                await directClient.Conversations.SendToConversationAsync(directReply);
            }
        }
    }

    public class TopScoringIntent
    {
        public string intent { get; set; }
        public double score { get; set; }
    }

    public class Intent
    {
        public string intent { get; set; }
        public double score { get; set; }
    }

    public class Entity
    {
        public string entity { get; set; }
        public string type { get; set; }
        public int startIndex { get; set; }
        public int endIndex { get; set; }
        public double score { get; set; }
    }

    public class LUISResponse
    {
        public string query { get; set; }
        public TopScoringIntent topScoringIntent { get; set; }
        public List<Intent> intents { get; set; }
        public List<Entity> entities { get; set; }
    }
}