# Fixity #

Is a basic FIX ([Financial Information eXchange](https://en.wikipedia.org/wiki/Financial_Information_eXchange)) server written using the  [Akka.NET](http://getakka.net/) Actor Model. Its purpose is not so much FIX but to enable the author to learn about and assess how the Actor Model and Akka.NET may help to develop such a server.

It could be used as a test harness for a FIX client, but is not suitable as a production FIX server in its existing state.

## What does it do? ##
On starting, the FIX server

 1. Listens for a TCP client connecting to the configured port (default 9700).
 2. On client connection the server waits for a Logon message from the client.
 3. On receiving a Logon message it replies to the client with its own Logon message.
 4. The client can send a Quote Request message (e.g. for a FX spot rate), and the server will respond with a Quote message.


 * When step 3 completes, the server sends heartbeat messages to the client at regular intervals. If the client does not respond with a heartbeat message within 2 * the configured heartbeat interval, the server considers the connection lost and disconnects (according to the FIX protocol the server should send a Test Request message to the client but this is not implemented).

### How does it do it? ###

*Add info about the conversion of FIX text into strongly typed messages, state machine, etc*

*Add a state diagram*

## How do I run it? ##
### Requirements ###

Platform: Windows with .NET 4.5

*(I'm not sure if Akka will work on the cross platform .NET Core)*

Dependencies(which are retrieved from NuGet automatically on build).

 * Akka.NET 1.0.4
 * NUnit 2.6.4

### Building and running ###

Either
1. Using Visual Studio 2015, open and build the solution src/Fixity.sln and run the FIXServer project, or
2. Run build.cmd and then run the resultant Fixity.FIXServer.exe

* Configuration
* How to run tests