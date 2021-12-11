using Godot;
using System;

public class ResendPacket
{
    // the relevant information for the packet
    public int recipientPort;
    public String recipientIP;    
    public int messageID = 0;
    float resendTime = 0.1f;
    public float resendTimer = 0.0f;
    float expiredTime = 0.6f;
    public float expiredTimer = 0.0f;
    public byte[] bytesToSend;

    public ResendPacket(int port, String ip, byte[] bytes){
        messageID = BitConverter.ToInt32(bytes, 0);
        recipientIP = ip;
        recipientPort = port;
        bytesToSend = bytes;
    }

    public bool resend(){        
        return resendTimer > resendTime;
    }

    public bool expired(){  
        // return true if the message has expired      
        if(expiredTimer > expiredTime){
            GD.Print($"Message {messageID} resender expired");
            return true;
        }
        return false;
    }
}
