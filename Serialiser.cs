using Godot;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;

public class Serialiser : Node
{
    Network network;
    MenuManager menu;   
    Game game;
    PlayerPacket pPacket;
    BulletPacket bPacket;
    public enum PacketType{ Ping, Pong, Str, PlayerTransform, IP, IPResponse, Bullet, StartGame, 
    BulletHit, PlayerHit, ConnectionUpdate, DisconnectedPlayer, MessageConfirmation, Connection, 
    PlayerDied, GameWon }

    public override void _Ready()
    {   
        // get the scene tree objects
        network = GetNode<Network>("../Network"); 
        menu = GetNode<MenuManager>("../MenuManager"); 
        game = GetNode<Game>("../Game"); 
    }

#region Serialize Functions
    public byte[] serialize(PlayerPacket _packet, PacketType _type, bool _confirm, int _clientID){
        // serialize a player transform packet
        int messageID = -1;
        // give the message an ID if it requires confirmation
        if(_confirm){
            network.nextPacketID++;
            messageID = network.nextPacketID;
        }
        // convert the data to a byte array
        var bytes = BitConverter.GetBytes(messageID)
        .Concat(BitConverter.GetBytes(_clientID)) 
        .Concat(BitConverter.GetBytes((int)_type))
        .Concat(BitConverter.GetBytes(_packet.clientID))
        .Concat(BitConverter.GetBytes(_packet.timeSent))
        .Concat(BitConverter.GetBytes(_packet.position.x))
        .Concat(BitConverter.GetBytes(_packet.position.y))
        .Concat(BitConverter.GetBytes(_packet.rotation))
        .Concat(BitConverter.GetBytes(_packet.velocity.x))
        .Concat(BitConverter.GetBytes(_packet.velocity.y));
        return bytes.ToArray();
    }

    public byte[] serialize(BulletPacket _packet, PacketType _type, bool _confirm, int _clientID){
        // serialize a bullet packet
        int messageID = -1;
        // give the message an ID if it requires confirmation
        if(_confirm){
            network.nextPacketID++;
            messageID = network.nextPacketID;
        }
        // convert the packet to a byte array
        var bytes = BitConverter.GetBytes(messageID)
        .Concat(BitConverter.GetBytes(_clientID)) 
        .Concat(BitConverter.GetBytes((int)_type))
        .Concat(BitConverter.GetBytes(_packet.clientID))
        .Concat(BitConverter.GetBytes(_packet.timeSent))
        .Concat(BitConverter.GetBytes(_packet.playerPosition.x))
        .Concat(BitConverter.GetBytes(_packet.playerPosition.y))
        .Concat(BitConverter.GetBytes(_packet.playerRotation))
        .Concat(BitConverter.GetBytes(_packet.velocity));
        return bytes.ToArray();
    }
    public byte[] serialize(String _message, PacketType _type, bool _confirm, int _clientID){
        // SERIALIZATION
        int messageID = -1;
        // give the message an ID if it requires confirmation
        if(_confirm){
            network.nextPacketID++;
            messageID = network.nextPacketID;
        }
        // convert the packet to a byte array
        var bytes = BitConverter.GetBytes(messageID)
        .Concat(BitConverter.GetBytes(_clientID))  
        .Concat(BitConverter.GetBytes((int)_type))       
        .Concat(Encoding.UTF8.GetBytes(_message));
        return bytes.ToArray();
    }   
    
    public byte[] serialize(float _x, PacketType _type, bool _confirm, int _clientID){
        // SERIALIZATION
        int messageID = -1;
        // give the message an ID if it requires confirmation
        if(_confirm){
            network.nextPacketID++;
            messageID = network.nextPacketID;
        }
        // convert the float to a byte array
        var bytes = BitConverter.GetBytes(messageID)
        .Concat(BitConverter.GetBytes(_clientID))  
        .Concat(BitConverter.GetBytes((int)_type))
        .Concat(BitConverter.GetBytes(_x));
        return bytes.ToArray();
    }
    public byte[] serialize(int _x, PacketType _type, bool _confirm, int _clientID){
        // SERIALIZATION
        int messageID = -1;
        // give the message an ID if it requires confirmation
        if(_confirm){
            network.nextPacketID++;
            messageID = network.nextPacketID;
        }
        // convert the int to a byte array
        var bytes = BitConverter.GetBytes(messageID)
        .Concat(BitConverter.GetBytes(_clientID))  
        .Concat(BitConverter.GetBytes((int)_type))
        .Concat(BitConverter.GetBytes(_x));
        return bytes.ToArray();
    } 

    public byte[] serialize(PacketType _type, bool _confirm, int _clientID){
        // SERIALIZATION
        int messageID = -1;
        if(_confirm){
            network.nextPacketID++;
            messageID = network.nextPacketID;
        }

        var bytes = BitConverter.GetBytes(messageID)
        .Concat(BitConverter.GetBytes(_clientID))  
        .Concat(BitConverter.GetBytes((int)_type));
        return bytes.ToArray();
    } 
    #endregion

    public void deserialize(UdpReceiveResult _result){
        // DESERIALIZATION        
        // get the ip from the recieved message
        String ip = _result.RemoteEndPoint.Address.ToString();
        // get the byte array from the buffer
        byte[] bArray = _result.Buffer.ToArray();      
        
        int index = 0;
        // take the message ID from the array and increment the index
        int messageID = BitConverter.ToInt32(bArray, index);  
        index += sizeof(int);
        
        // get the client ID
        int clientID = BitConverter.ToInt32(bArray, index);  
        index += sizeof(int);
        
        // if this is a message that needed confirmation and we've already handled this message, return 
        if(!network.isServer && messageID != -1 && network.handledMessage(messageID)){
            GD.Print($"Already handled message {messageID}");
            return;
        }

        network.resetClientTime(clientID);

        // check if we need to send a message confirmation
        bool sendConfirmation = false;
        if(messageID != -1)
            sendConfirmation = true;            

        // get the packet type and increment again
        PacketType type = (PacketType)BitConverter.ToInt32(bArray, index);  
        index += sizeof(int);
        
        // deserialize based on the packet type
        switch(type){
            // when the server recieves a ping, it will send back a pong
            case PacketType.Ping:
                int returnPort = BitConverter.ToInt32(bArray, index);
                network.send(returnPort, ip, menu.time, PacketType.Pong, false);                
                break;

            // when the client recieves a ping, it syncs up the in game timer with the server
            case PacketType.Pong:
                float serverTime = BitConverter.ToSingle(bArray, index);
                index += sizeof(Single);
                // set the client time to the server
                menu.setTime(serverTime);
                break;

            // used to send player position and rotation info to both the server and the players
            case PacketType.PlayerTransform:
                // unpack the array 
                pPacket.clientID = BitConverter.ToInt32(bArray, index);
                index += sizeof(int);
                pPacket.timeSent = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                pPacket.position.x = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                pPacket.position.y = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                pPacket.rotation = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                pPacket.velocity.x = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                pPacket.velocity.y = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                // push the new packet to the queue
                //network.playerPackets.Enqueue(pPacket);
                if(network.isServer){
                    if(network.IsNewPacket(pPacket.clientID, pPacket.timeSent)){
                        network.broadcast(bPacket.clientID, pPacket, PacketType.PlayerTransform, false);
                    }
                }else{
                    game.updatePlayer(pPacket);
                }

                break;

            case PacketType.Bullet:
                // deconstruct the bullet packet
                bPacket.clientID = BitConverter.ToInt32(bArray, index);
                index += sizeof(int);
                bPacket.timeSent = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                bPacket.playerPosition.x = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                bPacket.playerPosition.y = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                bPacket.playerRotation = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                bPacket.velocity = BitConverter.ToSingle(bArray, index);
                index += sizeof(float);
                // push the new packet to the queue
                //network.bulletPackets.Enqueue(bPacket);
                if(network.isServer){
                    network.broadcast(bPacket.clientID, bPacket, PacketType.Bullet, true);
                }else{
                    game.spawnBullet(bPacket);
                }

                break;

            // used for sending strings
            case PacketType.Str:
                // create a new array of the message size    
                byte[] messageArray = new byte[bArray.Count() - index];         
                // copies the byte array into the messageArray, starting at the index      
                Array.Copy(bArray, index, messageArray, 0, bArray.Count() - index);
                // convert the array to a string
                String message = Encoding.UTF8.GetString(messageArray);
                
                if(network.isServer){
                    // if we're the server, broadcast the message
                    network.broadcast($"Player {clientID + 1}: {message}", PacketType.Str, true);   
                    menu.addChatMessage($"Player {clientID + 1}: {message}");
                }else{
                    menu.addChatMessage(message);   
                }                 
                break;

            // when a client connected a message is sent 
            case PacketType.Connection:
                if(network.isServer){
                    int connectedClient = BitConverter.ToInt32(bArray, index);
                    index += sizeof(int);         
                    // broadcast the message that a client has connected           
                    network.broadcast(connectedClient, PacketType.Connection, true);
                }else{                    
                    int connectedClient = BitConverter.ToInt32(bArray, index);
                    index += sizeof(int);
                    GD.Print(connectedClient);
                    menu.addChatMessage($"Player {connectedClient + 1} has connected");
                }
                break;

            // when a client connects, it will send its ip and port to the server
            case PacketType.IP:
                int i;
                int port = BitConverter.ToInt32(bArray, index);
                // if there are still available slots in the game
                if(network.clientCount < network.maxClients && !game.playing){
                    i = network.clientCount;
                    GD.Print("Adding client");  
                    // add the port and ip to the ServerClient list                  
                    network.addClient(port, _result.RemoteEndPoint.Address);
                    menu.addChatMessage($"Added client to list at index {network.clientCount}");
                    GD.Print("Incoming client id " + clientID);
                }else{
                    // lobby full
                    i = -1;
                }
                
                // send back a response to the client with their player number
                network.send(port, ip, i, PacketType.IPResponse, true);
                break;

            case PacketType.IPResponse:
                int playerNum = BitConverter.ToInt32(bArray, index); 

                // check if the lobby was full
                if(playerNum == -1){
                    menu.addChatMessage("Unable to connect: Lobby full");
                    network.udpStop();               
                }else{
                    // if there was space in the lobby, set the playerNumber and sync the time
                    menu.setPlayerNumber(playerNum);
                    // tell the server to broadcast a message to say that the client has connected
                    network.send(Network.hostPort, network.serverIp, playerNum, PacketType.Connection, false);
                }
                break;

            case PacketType.StartGame:
                // if this network is the server
                if(network.isServer){
                    network.broadcast(network.clientCount, PacketType.StartGame, true);    
                    game.playing = true;
                }else{
                    int clientCount = BitConverter.ToInt32(bArray, index);
                    index += sizeof(int);
                    menu.startGame(clientCount);
                    menu.addChatMessage("Player 1 has started the game");
                }
                break;

            case PacketType.BulletHit: 
                // deserialize which player was hit by a bullet           
                int playerHit = BitConverter.ToInt32(bArray, index);
                index += sizeof(int);
                // broadcast it to clients
                network.broadcast(playerHit, PacketType.PlayerHit, true);
                break;

            case PacketType.PlayerHit:
                // decrement the health of the player that was hit 
                int pHit = BitConverter.ToInt32(bArray, index);
                index += sizeof(int);  
                
                game.decrementHealth(pHit);                
                break;

            case PacketType.PlayerDied:
                // if a player has died
                int pDied = BitConverter.ToInt32(bArray, index);
                index += sizeof(int); 

                // if we're the server
                if(network.isServer){
                    // tell all the clients that a player has died
                    network.broadcast(pDied, PacketType.PlayerDied, true);
                    // increment the server's count of how many player's are dead
                    game.playersDead++;
                    // if that number leaves on client alive
                    if(game.playersDead >= network.clientCount - 1){
                        // tell the clients that the game has been won
                        network.broadcast(0, PacketType.GameWon, true);
                    }else{
                        network.broadcast($"{network.clientCount - game.playersDead} players remain...", PacketType.Str, true);
                    }
                }else{
                    // if we're a client, check if that player is already dead
                    game.checkIfDead(pDied);                
                }
                break;

            case PacketType.GameWon:            
                // check which player won
                game.checkForWinner();               
                break;
            case PacketType.ConnectionUpdate:
                int clientNum = BitConverter.ToInt32(bArray, index);
                index += sizeof(int);  

                // if we're the server then reset the client's time since last packet
                if(network.isServer){                      
                    network.resetClientTime(clientNum);                
                }else{                    
                    // we've recieved a response from the server and thus the server is still listening
                    network.resetServerTime();
                }             
                break;

            // sent when a player has not sent a message to the server in x seconds
            case PacketType.DisconnectedPlayer:
                // get which player has disconnected
                int playerID = BitConverter.ToInt32(bArray, index);
                index += sizeof(int);
                
                if(network.isServer){
                    network.removeClient(playerID);
                }else{
                    game.removePlayer(playerID);
                    game.checkForWinner();
                }
                break;
            
            // if a packet required a confirmation, a message with this tag will be sent that contains the messageID
            case PacketType.MessageConfirmation:
                    int messageIDtoRemove = BitConverter.ToInt32(bArray, index);
                    index += sizeof(int);
                    network.removeResentPacket(messageIDtoRemove);
                break;

            default: 
                GD.Print("No type given");
                break;
        }

        // if the message received requires confirmation
        if(sendConfirmation){
            // send the message ID back to the sender
            if(!network.isServer){
                network.send(Network.hostPort, network.serverIp, messageID, PacketType.MessageConfirmation, false);
            }else{
                network.sendConfirmationToClient(messageID, clientID);
            }
        }

        

    }
}
