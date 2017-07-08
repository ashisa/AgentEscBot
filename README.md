---
platforms: Bot Framework
development language: C#
author: Ashish Sahu
---

#What is this code?

This is a bot application developed for Microsoft Bot Framework. The primary purpose of this speficic implementation is accommodate the escalation scenario with bots created with Microsoft Bot Framework.

This is a fully functional app that implements the following features -

+ Uses LUIS model to understand user intent
	+ At the moment, the LUIS model (available as well) just has the Escalation and Agent Registration intents
+ Maintains a register of agents available and users asking for escalations
	+ Uses In-Memory lists to maintain this information
+ Keeps a mapping of agents and users paired to deal with escalations
	+ A dictionary is utilized to keep this information available - again, In-Memory.
+ Allows agents to sign in to take on the escalations
	+ Anyone can sign in as an agent since we aren't performing any authorizations
+ Recognizes when users are requesting an escalation
	+ A LUIS signal triggers this with an explicit request from the users
+ Allows them to communicate with each other via the bot
	+ The messages a prefixed with the agent/user names to keep thing less complicated
+ Implements control messages for issue resolution and logging out
	+ Like others aspect, things are kept simple

Direct link to interesting code - https://raw.githubusercontent.com/ashisa/AgentEscBot/master/TestBot01/Escalation.cs
	
#Multi-channel

Keeping up with the multi-channel integration features of Bot Framework, this application allows the agents and users to use any available channel. The testing was done with WebChat, Skype, GroupMe and Microsoft Teams and it effectively means that the agents and users are free to use any of these channels and can still talk to each other seamlessly.

#How to use this?

Just clone this repository and do the following -

1. Create a new Bot on Bot Framework and add the Microsoft App Id/Password in the web.config file
2. Create a Luis Applicatiaon at http://luis.ai and import the Luis application found under LuisApp
3. Train this application, publish it and grab the Luis app id and key
4. Add the Luis app id and key to Escalation.cs class at line number 98
5. Publish this application and update the Bot to point to the correct messaging endpoint
6. Enable the channels as you like

That's it!

#How to test it?

Once the bot is up and running, bring up the messaging apps that you have enabled for your bot and do one the following activities -

1. Sign in as an agent by sending something this - **I am human agent**
2. As a user, send something that conveys a message similar to this - **escalate this please**
3. At this point, the agent and user should both receive the update about the escalation and any messages you send in the chat window will pipe between them
4. To keep things simple, the agent can just send IssueResolved to close the escalation
5. When it's time to go - the agents can also send LogMeOut to remove themselves from the agent roster.

#Other thoughts

This application, as a concept, uses in-memory data structure to demonstrate how a multi-channel human escalation scenario can work and as such, clearly cannot scale. The implementation, however, is simple enough to extend and use scalable ways to keep track of agents, users and the escalations in progress.

This implementation uses LUIS intent to trigger an escalation but a real world example will also need to account for other signals to look for opportunities to help users efficiently and proactively.

I have also taken the easier routes to have agents log in, log out or to mark an escalation resolved and appropriate authorization will need to be in place to keep things in check.

Feedback welcome!

