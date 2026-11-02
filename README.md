**Project / feature summary.**



Added a server authoritative health and respawn system.



**How to run and test the project.**



Repository: https://github.com/Merfij/Unity-Networking.git



1\. Download the project from repository.

2\. In Unity: Add / Add Project from disk.

3\. Locate downloaded project.

4\. Open the project.



Testing:



1\. When the project is loaded, build and run a standalone version, once loaded select "Start Client (localhost).

2\. In Unity editor, enter play mode and select "Start Host".

3\. Both the host and the client should see each other now.

4\. Each player starts out with 100HP and a projectile hit does 25HP damage. So tanks die after 4 hits.

5\. Once health is zero, movement and shooting are disabled.

6\. Press "R" to respawn.

7\. Tank respawns at starting position with full health.



**What you implemented or changed.**



I have implemented a health and respawn system which allows players to take damage and respawn upon health reaching zero. The client sends a respawn request to the server which then validates that request and spawns a new tank at the starting location.



**Important client/server, authority and replication decisions.**



The client is responsible for sending movement, shooting and respawn requests while the server is responsible for validating these requests and controlling the game state and replicating the authoritative game state to the clients.

Since health and death are network variables only the server has the authority to changes these values so clients can see these values but can never change them by themselves.



**The network conditions used for testing.**



I have tried different versions, both without network simulation and with and the respawn function worked as intended under both conditions.



Network conditions:



\- Packet delay Range: 100ms

\- Packet Delay: 100ms

\- Packet Jitter: 0

\- Packet Loss: 10%

\- Packet Loss Interval: 0



**At least one networking/synchronization problem you encountered and how you diagnosed it.**



I did not encounter a problem in these areas, my biggest hurdle in this project was understanding the structure and what components where responsible for what and how I should implement the health and respawn system so that only the server decides the authoritative state. It was a lot of reading and analyzing how components pass along information to server before I could start working on the system.



**The networking improvement or optimization you made, including why you chose it.**



I chose the health and respawn system in order to make this project have a game loop:



&#x20;- Start

&#x20;- Move / Shoot

&#x20;- Die

&#x20;- Restart



I wanted to see how this would work in a multiplayer setting as that is something I had never done before and wanted to see how it would work.



**Known limitations, trade-offs or remaining problems.**



The project has only been testing locally on the same computer using the host and client set-up so networking issues could happen if tested across different computers or over the internet.

