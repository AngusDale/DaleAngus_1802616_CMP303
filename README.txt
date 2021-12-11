Angus Dale - 1802616
CMP303 Coursework

On start-up:
 - Open multiple instances of the "test" exe.
 - One window will act as the server.
 - Check the "Is Server" checkbox in the menu, and then click "Connect". This will create a server that is listening for incoming messages.
 
 - If more than one instance of the game (excluding the server) is running on the same machine:
	- Enter a unique port into the text box for each window. (One window may be left blank, it will make use of the default client port)
	- Click connect

 - If you are running the game on multiple machines:
	- One machine must host the server AND their onwn instance of the game (you do not need to enter a server IP or port for the same machine)
	- The user hosting the server must share the ip in the top left of the screen with the other player
	- The remote player must enter the IP they have been give into the "Server IP" box.
	- They do not need to enter a port unless they are running more than one instance of the game on the same machine.

- Once connected to the serverr, you should be given a player number from 1 to 4. The server will accept up to 4 players and reject any other incoming
  connections.

- Player one has the ability to start the game by clicking the "start" button. This will start then game for all clients.


- GAME CONTROLS:
	- WASD to move
	- Left mouse button to shoot
	- Cursor position dictates shooting direction
	- "Add Time" adds a second to the timer to demonstrate the client's time syncing with the server
	
- Left clicking on the chat bar in the bottom left allows the user to send messages to other players
- "Enable Disconnnet", enables the disconnect button (to prevent accidental presses while playing)
- Disconnect will close the client's udpClient and send a message to the server saying they have disconnected.