using Godot;
using System;
using System.Net;
public class MenuManager : Node
{
    Network network;
    Game game;
    Camera2D camera;

    // UI buttons
    Button submitButton;
    Button connectButton;
    Button addSecondButton;
    Button syncButton;
    Button startButton;
    Button disconnectButton;
    CheckBox disconnectCheckbox;
    CheckBox serverCheckBox;
    // UI labels and chat window
    Label timeText;    
    Label localIpLabel;    
    Label serverIpLabel;    
    Label clientPortLabel;    
    Label playerNumberLabel;   
    Label pingLabel;    
    RichTextLabel chatBox;

    // text bar editors
    LineEdit chatBar;
    TextEdit portTextEdit;
    TextEdit ipTextEdit;    

    public ProgressBar healthBar;

    public float time = 0.0f;
    float pingTime = 0.0f;    
    float pingTimeout = 3.0f;
    float syncTimeTimer = 0.0f;

    bool pinging = false;    
    bool playingGame = false;
    bool canDisconnect = false;
    string externalIpString;
    public int playerNumber;
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {      
        // get the buttons and labels etc from the scene tree
        network  = GetNode<Network>("../Network"); 
        game     = GetNode<Game>("../Game");
        connectButton   = GetNode<Button>("Canvas/Connect");
        addSecondButton = GetNode<Button>("Canvas/AddSecond");
        startButton     = GetNode<Button>("Canvas/Start");
        disconnectButton     = GetNode<Button>("Canvas/Disconnect");
        serverCheckBox  = GetNode<CheckBox>("Canvas/isServer"); 
        disconnectCheckbox   = GetNode<CheckBox>("Canvas/canDisconnect"); 
        timeText            = GetNode<Label>("Canvas/Time");        
        localIpLabel        = GetNode<Label>("Canvas/LocalIp");
        clientPortLabel     = GetNode<Label>("Canvas/Port");
        serverIpLabel       = GetNode<Label>("Canvas/IP");
        playerNumberLabel   = GetNode<Label>("Canvas/PlayerNumber");
        pingLabel   = GetNode<Label>("Canvas/Ping");
        chatBox   = GetNode<RichTextLabel>("Canvas/Chat");
        healthBar = GetNode<ProgressBar>("Canvas/HealthBar");
        healthBar.Hide();
        chatBar         = GetNode<LineEdit>("Canvas/LineEdit");
        ipTextEdit      = GetNode<TextEdit>("Canvas/IPTextEdit");
        portTextEdit    = GetNode<TextEdit>("Canvas/PortTextEdit");

        // connect the buttons to their functions
        addSecondButton.Connect("pressed", this, "addSecond");
        connectButton.Connect("pressed", this, "connect");
        serverCheckBox.Connect("pressed", this, "flipServer");
        disconnectCheckbox.Connect("pressed", this, "flipDisconnect");
        startButton.Connect("pressed", this, "sendStartGame");
        disconnectButton.Connect("pressed", this, "disconnectClient");

        // obtaining the external ip address https://stackoverflow.com/questions/3253701/get-public-external-ip-address
        externalIpString = new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
        var externalIp = IPAddress.Parse(externalIpString);

        // set the text label for the external ip
        localIpLabel.Text = "IP: " + externalIp.ToString();

        chatBox.ScrollFollowing = true;
    } 

    public override void _Process(float _delta){
        // increase the timer by the time since the last frame
        time += _delta; 
        syncTimeTimer += _delta;  
        // output the time to the label to two decimal places
        timeText.Text = "Time: " + String.Format("{0:.##}", time);

        // send a message to the chat when the player presses enter
        if(Input.IsActionJustPressed("Enter") && !chatBar.Text.Empty()){
            sendChatMessage();
        }        

        if(canDisconnect){
            disconnectButton.Disabled = false;
        }else{
            disconnectButton.Disabled = true;
        }

        // ping the server every second to keep the timer consistent and update the ping
        if(network.connected && syncTimeTimer >= 1.0f){
            syncTime();
            syncTimeTimer = 0.0f;
        }

        // increment the pinging timer if the client is currently pining the server
        if(pinging)
            pingTime += _delta;

        // if the ping timer excedes 3 seconds, close the client
        if(pingTime > pingTimeout){      
            GD.Print("Ping timed out...");
            pinging = false;
            pingTime = 0.0f;
        }        

        // if the game is playing, return
        if(!game.playing){
            // if the game is not playing, set the menu buttons
            setMenuButtons();            
        }
    }

    void flipServer(){
        // this flips when the isServer checkbox is clicked
        network.isServer = !network.isServer;
    }
    void flipDisconnect(){
        // this flips when the isServer checkbox is clicked
        canDisconnect = !canDisconnect;
    } 
    void connect(){
        addChatMessage("Connecting...");
        // create a udpClient
        network.connect();
        // sync time with server if current instance is a client  
        if(!network.isServer && network.connected){      
            sendPlayerIpAndPort(); 
        }
    }
    void disconnectClient(){
        network.send(Network.hostPort, network.serverIp, playerNumber, Serialiser.PacketType.DisconnectedPlayer, true);
        network.udpStop();
    }
    public void syncTime(){
        // sync the timer with the server
        if(!network.isServer){        
            pinging = true;
            pingTime = 0.0f;                         
            //GD.Print("Sending time to host");
            // pings must be sent with the client port incase clients are being run on the same machine
            network.send(Network.hostPort, network.serverIp, network.clientPort, Serialiser.PacketType.Ping, false);
        }
    }
    public void setTime(float _serverTime){
        pinging = false;     
        time = _serverTime + pingTime / 2.0f;
        //GD.Print("Time: " + time + " Server time: " + _serverTime + " Pinging time: " + pingTime);
        pingLabel.Text = "Ping: " + (int)((pingTime / 2.0f) * 1000.0f) + "ms";
        //addChatMessage($"Ping Time: {pingTime}");
    }
    void sendPlayerIpAndPort(){
        // try catch only relevant to clients running on the same machine attempting to use the same port
        try{
            GD.Print("Sending ip to host");
            // send the server the current clients ip and port
            network.send(Network.hostPort, network.serverIp, network.clientPort, Serialiser.PacketType.IP, false);
        }catch(NullReferenceException e){
            GD.Print(e.Message);
        }
    }
    void sendStartGame(){
        if(network.connected){
            // upon button press, send a message to the server to begin the game
            network.send(Network.hostPort, network.serverIp, network.clientPort, Serialiser.PacketType.StartGame, true);
        }
    }
    public void startGame(int _clientCount){   
        healthBar.Show();
        // once a response from the server has been recieved witht the startGame flag, the game will begin 
        if(playerNumber != -1){            
            // hide menu buttons
            hideButtons();            
            // set the client to a player object
            if(!network.isServer)
                for(int i = 0; i < 4; i++){
                    // only display the players that are connected
                    if(i >= _clientCount)
                        game.player[i].isDead = true;
                        
                    if(playerNumber == i)
                        game.player[i].isClient = true;                      
                }
            // start game
            game.begin(playerNumber);
        }
    }
    bool isInt(String _str){
        // check if a string is an int
        try {
    	    int x = int.Parse(_str);
      	    return true; //String is an Integer
	    } catch (FormatException e) {
    	    return false; //String is not an Integer
	    }
    }    
    public void setPlayerNumber(int _i){
        network.connected = true;
        playerNumber = _i;    
        network.clientID = _i;
        GD.Print("You are player " + (_i + 1));    
        playerNumberLabel.Text = "Player " + (_i + 1);    
    }
    void sendChatMessage(){
        network.send(Network.hostPort, network.serverIp, chatBar.Text, Serialiser.PacketType.Str, true);
        chatBar.Text = "";
    }
    public void addChatMessage(String _message){        
        chatBox.GetVScroll().Visible = false;
        chatBox.Newline();
        chatBox.AddText(_message);        
    }
    void hideButtons(){
        if(!network.isServer){
            connectButton.Hide();
            serverCheckBox.Hide();
            addSecondButton.Hide();
            startButton.Hide();
            ipTextEdit.Hide();
            portTextEdit.Hide();
            clientPortLabel.Hide();
            playerNumberLabel.Hide();
            serverIpLabel.Hide();
            startButton.Hide();
        }
             
        serverCheckBox.Disabled = true;
        addSecondButton.Disabled = true;
        connectButton.Disabled = true;
        startButton.Disabled = true;
    }
    void setMenuButtons(){
        
        // sets the necessary buttons when the isServer bool is flipped

        if(network.connected){
            // if we are connected, disable the connect button  
            connectButton.Disabled = true;   

            // player number is output to the screen as +1 to avoid "player number: 0"
            if(playerNumber == 0){
                startButton.Disabled = false;    
                
            }else{
                startButton.Disabled = true;
            }

            serverCheckBox.Hide();
            if(!network.isServer)
                localIpLabel.Hide();
        }else{
            // enable the connect button
            connectButton.Disabled = false;              
            startButton.Disabled = true; 

            // if we're not the server
            if(!network.isServer){
                // check for input in the ip and port text editors
                if(!string.IsNullOrEmpty(portTextEdit.Text) && isInt(portTextEdit.Text))    
                    network.clientPort = portTextEdit.Text.ToInt();
                if(!ipTextEdit.Text.Empty()) {
                    network.serverIp = ipTextEdit.Text;               
                }
                else {
                    network.serverIp = "127.0.0.1";
                }

                if(!isInt(portTextEdit.Text))
                    portTextEdit.Text = "";


                // shor the relevant labels 
                ipTextEdit.Show();
                portTextEdit.Show();
                clientPortLabel.Show();
                serverIpLabel.Show();
                playerNumberLabel.Show();                  
                startButton.Show();
                
            }else{
                // if we are the server, hide all non relevant labels and buttons
                startButton.Hide();
                ipTextEdit.Hide();
                portTextEdit.Hide();
                clientPortLabel.Hide();
                serverIpLabel.Hide();
                playerNumberLabel.Hide();           
            }
        }
    }
    void addSecond(){
        time += 1.0f;
    }
}

