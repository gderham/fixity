# Fixity #

Is a very basic FIX ([Financial Information eXchange](https://en.wikipedia.org/wiki/Financial_Information_eXchange)) server written in C# using the [Akka.NET](http://getakka.net/) Actor Model. Its purpose is not so much FIX-related but to enable the author to learn how the Actor Model and Akka.NET may help to develop such a thing.

## What does it do? ##

* Listens for and accepts a FIX (TCP) client.
* Exchanges Logon messages with the client.
* Exchanges repeated Heartbeat messages with the client.
* Replies to any QuoteRequest message from the client with a Quote message containing the requested instrument price.
* If the client fails to send a Heartbeat message, a Test Request message is sent and if still no reply the server is shut down.

## How does it do it? ##

* FixServerActor creates a TcpServerActor, which listens for TCP clients.
* ASCII FIX messages received by the TcpServerActor are translated into subclasses of BaseMessage by the FixInterpreterActor before forwarding to the FixServerActor.
* Typed FIX messages sent by the FixServerActor are translated to ASCII FIX messages by the FixInterpreterActor before forwarding to the TcpServerActor.
* FixServerActor and TcpServerActor handle connection, logon state etc by operating as state machines - changing their message handling behaviour depending on state.

## How do I run it? ##
### Requirements ###

Platform: Windows / .NET 4.5

Dependencies (retrieved from NuGet):

 * Akka.NET 1.0.4
 * xUnit.net 2.1.0
 * Fluent Assertions 4.0.0
 * log4net 1.2.13

### Building and running ###

Either:

1. Using Visual Studio 2015, open and build the solution src/Fixity.sln and run the FIXServer project, or
2. Run src/build.cmd and run the resultant Fixity.FixServer.exe

### Configuration ###
Instrument prices used for quoting are passed as a dictionary of Symbol->Price into the TcpServerActor.

### Interacting with the FIX server ###
1. Connect to the FixServer on port 9700 (default) using a TCP client.
2. Log on by sending a Logon message:
`8=FIXT1.1\x019=35\x0135=A\x0149=Client\x0156=Bank\x0134=1\x01108=30\x0110=70\x01`
3. Send a heartbeat message: `8=FIXT1.1\x019=28\x0135=0\x0149=Client\x0156=Bank\x0134=1\x0110=253\x01`
4. Request a quote: `8=FIXT1.1\x019=71\x0135=R\x0149=Client\x0156=Bank\x0134=7\x01131=rfq712\x0155=USDJPY\x0110=171\x01`
5. Logout: `8=FIXT1.1\x019=28\x0135=5\x0149=Client\x0156=Bank\x0134=1\x0110=2\x01`

### Notes ###
1. Separation of states from transitions aids code legibility
  * E.g. state methods such as `FixServerActor.LoggedOn()` contain Receive methods *only*, any change of state is performed by a transition method e.g. `FixServerActor.BecomeLoggedOn()`.
2. Using constructor injection to pass an actor reference into another actor facilitates unit testing and loose coupling. But it means the parent-child relationship isn't formed - which may or may not be what is required. To enable full control, injecting a `Func<IActorRefFactory, IActorRef>` allows the parent actor constructor to instantiate the child actor using its Context, or the calling code (e.g. a unit test) can pass in a function that uses the ActorSystem.
3. IgnoreMessages doesn't affect messages already in an actor's mailbox - only those arriving afterwards - this means I tend to use `FishForMessage()` assertions where I'd rather use `ExpectMsg()`.

### Improvements (to do) ###
1. Reduce size of FixServerActor by splitting logic into separate child actors:
  1. HeartbeatActor - sends heartbeats and handles loss of client heartbeat.
  2. MessageSequenceActor - checks incoming messages are contiguous and handles resend requests.
  3. FixSessionActor - allow multiple simultaneous client connections - the FixServer creates a new FixSessionActor per client.
  4. QuotingActor - handle all quoting (subscribing to source rather than config).
2. Validation of messages (check checksum).
3. Replace the TcpServerActor with Akka.IO.
4. Use the FSM base actor - should simplify testing by setting actor state explicitly rather than by messages causing state transitions.
