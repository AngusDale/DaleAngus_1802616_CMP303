using Godot;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

public class Network : Node
{  
    // the information the server stores for each client connected
    public class ServerClient{
        public int id;
        public IPAddress ip;
        public int port;
        public float timeOfLastPositionUpdate;
        public float timeSinceLastPacket;
        public bool connected;
        public ServerClient(int _id, IPAddress _ip, int _port){
            id = _id;
            ip = _ip;
            port = _port;
            timeSinceLastPacket = 0.0f;
            connected = true;
        }
        // just removed timebetweenlastpackets so now need to make new storage for that and use it
    }

    #region Variables      
    
    UdpClient udpClient = null; 
    // other game objects in the scene tree
    Serialiser serialiser;
    MenuManager menu;    
    public Game game;  


    public bool connected = false;
    public bool isServer = false;
    public int clientPort = 9000; // default client port (will be changed if there are multiple instances of the game on one machine)
    public const int hostPort = 8000;
    public int clientCount = 0; // publicly accessible number of clients connected
    public int maxClients = 4;
    public int clientID;
    public String serverIp;   

    List<ServerClient> clients = new List<ServerClient>(); // used by the server to store a list of the ServerClients
    List<ResendPacket> resendPackets = new List<ResendPacket>(); // a list of packets that need to be resent until confirmation is received
    public List<int> handledPacketIDs = new List<int>(); // a list of the most recent message ids received

    int maxHeldIds = 75; // max number of previous message ids held
    public int nextPacketID = 0; // increments after every message sent
    float updateTimer = 0.0f;
    float clientTimeout = 2.0f;
    float serverTimeoutTimer = 0.0f;
    float serverTimeout = 2.0f;

    #endregion

    public override void _Ready()
    {   
        // obtain the other objects from the scene tree    
        menu = GetNode<MenuManager>("../MenuManager");
        serialiser = GetNode<Serialiser>("../Serialiser");
        game = GetNode<Game>("../Game");       
    }  

    public override void _Process(float _delta){   
        // update the timers
        updateTimer += _delta;
        serverTimeoutTimer += _delta;
        
        // check if any packets need to be re-sent, or have expired
        updateResendList(_delta);

        // remove the first 25 elements in the message ID list
        if(handledPacketIDs.Count > maxHeldIds){
            handledPacketIDs.RemoveRange(0, maxHeldIds / 2);
        }

        // reset the resend packet ID counter
        if(nextPacketID > 999)
            nextPacketID = 0;

        // if we're the server
        if(isServer){
            // see if any clients have not sent a packet recently
            checkForDisconnectedClients();

            // increment the time since last packet counter for each client
            foreach(ServerClient client in clients){
                client.timeSinceLastPacket += _delta;
            }

            // if we're the server, send a packet every 0.2 seconds to every client
            if(updateTimer > 0.1f){                
                // send each client a connectionn update to say that the server is still listening
                broadcast(0, Serialiser.PacketType.ConnectionUpdate, false);                
                updateTimer = 0.0f;
            }

        }else{ 
            // if we're the client and haven't recieved a message from the server in 10 seconds           
            if(serverTimeoutTimer > serverTimeout && game.playing){
                // disconnect
                udpStop();
            }

            // if we're the client, send a packet to the server every 0.2 seconds
            if(updateTimer > 0.1f && connected){
                send(hostPort, serverIp, menu.playerNumber, Serialiser.PacketType.ConnectionUpdate, false);
                updateTimer = 0.0f;
            }
        }
    }
    
    public void connect(){   
        // attempts to create a udp client
        try{
            if(udpClient == null)   
                // open a client with the relevant port
                if(isServer){  
                    udpClient = new UdpClient(hostPort);
                } else{
                    udpClient = new UdpClient(clientPort);
                }    
            GD.Print("UdpClient created...");    
            menu.addChatMessage("Client created...");    

            connected = true;            
            
            Task.Run(udpListen);

        }catch(SocketException e){
            if(e.ErrorCode == (int)SocketError.AddressAlreadyInUse){
                GD.Print("Error: Port already in use...");                            
                udpStop();               
            }                
        }
    }

    // the async function that accepts incoming datagrams
    public async void udpListen(){
        //string loggingEvent = "";
        try{       
            while(udpClient != null && connected){
                var receivedResults = await udpClient.ReceiveAsync();
                serialiser.deserialize(receivedResults);                             
            }            
        }
        catch(SocketException e){
            GD.Print("Socket Exception: " + e.Message + " " + e.ErrorCode);

            // error code, connection reset
            if(isServer && e.ErrorCode == (int)SocketError.ConnectionReset){
                // if we're the server, continue to listen             
                await Task.Run(udpListen);
                return;
            }else{
                // if we're the client, disconnect
                udpStop();
            }                
        // necessary catches for using receiveAsync as await does not return anything so ann error will be thrown when closing the udpclient
        }catch(ObjectDisposedException e){
            GD.Print(e.Message);
        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }      
    }

    public void udpStop(){
        connected = false;
        // check if the client exists and then close it
        if(udpClient != null)
            udpClient.Close();        

        udpClient = null;
        GD.Print("UdpClient closed...");
        GetTree().ReloadCurrentScene();
    }

    public void addClient(int _port, IPAddress _ip){      
        // adds a client to the list and increments the clientCount  
        ServerClient client = new ServerClient(clients.Count, _ip, _port);
        clients.Add(client);
        clientCount++;
    }
    // resets a client's time since last packet
    public void resetClientTime(int _id){
        // looping through the list instead of going to index 'clientID' incase a client is removed from the list
        for(int i =0; i < clients.Count; i ++){
            if(clients[i].id == _id){
                clients[i].timeSinceLastPacket = 0.0f;
                return;
            }
        }
    }
    public void resetServerTime(){
        serverTimeoutTimer = 0.0f;
    }
    public bool IsNewPacket(int _id, float _timeSent){
        
        // find the client that sent the update
        for(int i = 0; i < clients.Count; i++){
            if(i == _id){                
                // if this is the most recent packet
                if(_timeSent > clients[i].timeOfLastPositionUpdate){                    
                    // update the most recent packet time
                    clients[i].timeOfLastPositionUpdate = _timeSent;
                    return true;
                }else{                    
                    // else, this packet is out of order
                    GD.Print("Packet sent was out of order. Packet discarded");
                    return false;
                }
            }
        }
        return false;
    }

    // checks if a packet needs to be resent or has expired
    void updateResendList(float _delta){
        try{
            for(int i = 0; i < resendPackets.Count; i ++){
                // update both timers on the resend packet
                resendPackets[i].resendTimer += _delta; 
                resendPackets[i].expiredTimer += _delta; 

                // if the packet needs to be resent
                if(resendPackets[i].resend()){
                    // resend the packet's data 
                    udpClient.Send(resendPackets[i].bytesToSend, resendPackets[i].bytesToSend.Length, 
                        resendPackets[i].recipientIP, resendPackets[i].recipientPort);

                    GD.Print($"Resent message {resendPackets[i].messageID}");
                    // reset the packet's resend timer
                    resendPackets[i].resendTimer = 0.0f;
                }

                // if the resend packet has been in the list for more than x seconds remove it from the list
                if(resendPackets[i].expired()){                
                    resendPackets.Remove(resendPackets[i]);
                }
            }
        }catch(Exception e){
            GD.Print(e.Message);
        }
    }

    public void removeResentPacket(int _messageID){
        // loop through the packets
        for(int i= 0; i < resendPackets.Count; i ++){
            // remove the one that has been received from the list
            if(resendPackets[i].messageID == _messageID){
                //GD.Print("Resend packet removed");
                resendPackets.Remove(resendPackets[i]);
            }
        }
    }
    public void addResendPacket(int _port, String _ip, byte[] _arr){   
        // add a resend packet with the ip and port of the recipient, as well as the bytes that need to be sent
        ResendPacket packet = new ResendPacket(_port, _ip, _arr);
        resendPackets.Add(packet);      
    }
    void checkForDisconnectedClients(){
        for(int i = 0; i < clients.Count; i ++){              
        // check all the clients to see if x seconds have elapsed since recieving a packet from that client              
            if(clients[i].timeSinceLastPacket > clientTimeout){                
                // if so, connection lost so remove the client from the list   
                menu.addChatMessage($"No connection update from client {clients[i].id}, removing from client list"); 
                removeClient(clients[i].id);
            }
        }
    }

    public void removeClient(int _index){
        for(int i = 0; i < clients.Count; i ++){
            if(clients[i].id == _index){
                // remove the client from the list
                clients.Remove(clients[i]);
                clientCount--;
                // broadcast to the clients which player has disconnected
                broadcast(_index, Serialiser.PacketType.DisconnectedPlayer, true);
                menu.addChatMessage($"Client {_index} has disconnected");
            }
        }

        if(clientCount == 0){
            if(game.playing){                
                menu.addChatMessage("Game ended");
            }

            game.playing = false;
        }
    }

    public void sendConfirmationToClient(int _messageID, int _id){
        // send a confirmation message to a client that requested one in response to an important packet/message
        for(int i = 0; i < clients.Count; i ++){
            if(clients[i].id == _id){
                send(clients[i].port, clients[i].ip.ToString(), _messageID, Serialiser.PacketType.MessageConfirmation, false);
                return;
            }
        }
    }

    public bool handledMessage(int _msgID){
        // returns true if this client has already handled a message of this id

        // check the most recent packet IDs to see if we've already handled the packet with this message
        for(int i = 0; i < handledPacketIDs.Count; i++){
            // if we've already handled the packet
            if(handledPacketIDs[i] == _msgID)
                return true;
        }
        
        // if we've not seen this message before, add it to the list of handled messages
        handledPacketIDs.Add(_msgID);
        return false;
    }

    #region Send Functions

    public void send(int _port, String _ip, String _message, Serialiser.PacketType _type, bool _confirm){ 
        try{
            // serialize and send a packet/message to the ip and port specified      
            byte[] bytes = serialiser.serialize(_message, _type, _confirm, menu.playerNumber);
            udpClient.Send(bytes, bytes.Length, _ip, _port);     
            
            // if this message requires confirmation, add it to the resend list
            if(_confirm)
                addResendPacket(_port, _ip, bytes);
        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }
    }
    public void send(int _port, String _ip, PlayerPacket _packet, Serialiser.PacketType _type, bool _confirm){
        try{
            // serialize and send a packet/message to the ip and port specified      
            byte[] bytes = serialiser.serialize(_packet, _type, _confirm, menu.playerNumber);
            udpClient.Send(bytes, bytes.Length, _ip, _port);        

            // if this message requires confirmation, add it to the resend list
            if(_confirm)
                addResendPacket(_port, _ip, bytes);
        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }
    }
    public void send(int _port, String _ip, BulletPacket _packet, Serialiser.PacketType _type, bool _confirm){
        try{
            // serialize and send a packet/message to the ip and port specified      
            byte[] bytes = serialiser.serialize(_packet, _type, _confirm, menu.playerNumber);
            udpClient.Send(bytes, bytes.Length, _ip, _port);        

            // if this message requires confirmation, add it to the resend list
            if(_confirm)
                addResendPacket(_port, _ip, bytes);
        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }
    }
    public void send(int _port, String _ip, float _x, Serialiser.PacketType _type, bool _confirm){     
        try{
            // serialize and send a packet/message to the ip and port specified      
            byte[] bytes = serialiser.serialize(_x, _type, _confirm, menu.playerNumber);        
            udpClient.Send(bytes, bytes.Length, _ip, _port);        

            // if this message requires confirmation, add it to the resend list
            if(_confirm)
                addResendPacket(_port, _ip, bytes);        
        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }
    }
    public void send(int _port, String _ip, int _x, Serialiser.PacketType _type, bool _confirm){
        try{
            // serialize and send a packet/message to the ip and port specified      
            byte[] bytes = serialiser.serialize(_x, _type, _confirm, menu.playerNumber);
            udpClient.Send(bytes, bytes.Length, _ip, _port);        

            // if this message requires confirmation, add it to the resend list
            if(_confirm)
                addResendPacket(_port, _ip, bytes);  
        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }
    }
    public void send(int _port, String _ip, Serialiser.PacketType _type, bool _confirm){
        try{
            // serialize and send a packet/message to the ip and port specified      
            byte[] bytes = serialiser.serialize(_type, _confirm, menu.playerNumber);
            udpClient.Send(bytes, bytes.Length, _ip, _port);  

            // if this message requires confirmation, add it to the resend list
            if(_confirm)
                addResendPacket(_port, _ip, bytes); 

        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }
        
    }    
    public void broadcast(int _x, Serialiser.PacketType _type, bool _confirm){
        // send a message to all clients
        for(int i = 0; i < clients.Count; i ++){
            send(clients[i].port, clients[i].ip.ToString(), _x, _type, _confirm);
        }
    }
    public void broadcast(String _msg, Serialiser.PacketType _type, bool _confirm){
        // send a message to all clients
        for(int i = 0; i < clients.Count; i ++){
            send(clients[i].port, clients[i].ip.ToString(), _msg, _type, _confirm);
        }
    }
    public void broadcast(int _exclude, PlayerPacket _packet, Serialiser.PacketType _type, bool _confirm){
        // used to send a player packet or bullet packet straight to the clients without unpacking the array
        for(int i = 0; i < clients.Count; i ++){
            if(i != _exclude)
                send(clients[i].port, clients[i].ip.ToString(), _packet, _type, _confirm);  
        }
    }
    public void broadcast(int _exclude, BulletPacket _packet, Serialiser.PacketType _type, bool _confirm){
        // used to send a player packet or bullet packet straight to the clients without unpacking the array
        for(int i = 0; i < clients.Count; i ++){
            if(i != _exclude)
                send(clients[i].port, clients[i].ip.ToString(), _packet, _type, _confirm);  
        }
    }

    #endregion
    
    // public string receive(bool isServer){
    //     IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
    //     byte[] recieveBytes = udpClient.Receive(ref endPoint);
    //     string returnData = Encoding.UTF8.GetString(recieveBytes);

    //     if(isServer)
    //         send(clientPort, "Message received: " + returnData, "s/");
    //     else
    //         send(hostPort, "Message received: " + returnData, "s/");

    //     return returnData;        
    // }

}     

