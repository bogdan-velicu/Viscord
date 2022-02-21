Discord Ripoff written in C#

This is an unfinished project, the code isn't polished and it's a little messy plus it's not bug free, but maybe you can use some of the tehniques for your own projects.

Clients are not based on peer to peer connection, everything is handled trough the server for security reasons (logins, messages, ... )

Features:
- voice chat (using UDP packets + NAudio library for capturing microphone)
- messages with timestamps (TCP)
- file sharing
- link detection
- servers with channels for messages / voice

To-do:
- encrypt packets
- private user calls
- share screen using UDP
