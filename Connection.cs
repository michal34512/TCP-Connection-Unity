using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Linq;

public class Connection : MonoBehaviour
{
    public enum ConnectionRole
    {
        Idle, Server, Client
    }
    public static Connection Instance;

    private int _bufferSize = 1024;
    private byte[] _buffer;

    ConnectionRole _Role = ConnectionRole.Idle;
    /// <returns>
    /// Current connection state:
    /// <para>Idle -> client not connected</para>
    /// <para>Server -> server mode</para>
    /// <para>Client -> client mode</para>
    /// </returns>
    public static ConnectionRole Role
    {
        get
        {
            if(Instance!=null)
                return Instance._Role;
            return ConnectionRole.Idle;
        }
    }

    private TcpClient _TcpClient;
    private NetworkStream _NetworkStream;
    private TcpListener _listener;

    //Message box
    private List<byte[]> _MessBox = new List<byte[]>();

    //PUBLIC FUNCTIONS
    /// <returns> Returns value from player prefs. If doesn't exist then returns 7777.</returns>
    public static int Port
    {
        get
        {
            return PlayerPrefs.GetInt("Port", 7777);
        }
        set
        {
            PlayerPrefs.SetInt("Port", value);
        }
    }
    /// <returns> Returns value from player prefs. If don't exist then returns 127.0.0.1.</returns>
    public static string IpServ
    {
        get
        {
            return PlayerPrefs.GetString("IpServ", "127.0.0.1");
        }
        set
        {
            PlayerPrefs.SetString("IpServ", value);
        }
    }
    /// <summary> Function used to establish connection (for both server and client) </summary>
    /// <param name="role">Connection mode: Server / Client</param>
    public static void Start_Connection(ConnectionRole StartAs)
    {
        if (Instance != null)
        {
            Instance._Stop();
            try
            { 
                if (StartAs == ConnectionRole.Server)
                {
                    Debug.Log("Starting server at: " + IPAddress.Any.ToString() + ":" + Port.ToString());
                    Instance._listener = new TcpListener(IPAddress.Any, Port);
                    Instance._listener.Start();
                    Instance._listener.BeginAcceptTcpClient(Instance._Server_Callback, null); // Calling "_Server_Callback" when client connect
                }
                else if (StartAs == ConnectionRole.Client)
                {
                    Debug.Log("Connecting at: " + IpServ + ":" + Port.ToString());
                    Instance._TcpClient = new TcpClient();
                    Instance._TcpClient.BeginConnect(IpServ, Port, Instance._Client_Callback, null); //Connecting to server
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(":( Error while starting the connection. Error Code: " + ex);
            }
        }
    }
    /// <summary> Stopping current connection & setting Role to "Idle"</summary>
    public static void Stop_Connection()
    {
        if (Instance != null)
        {
            Instance._Stop();
        }
    }
    /// <returns> If connection is active returns true</returns>
    public static bool isConnected
    {
        get
        {
            try
            {
                if (Instance != null && Instance._TcpClient != null && Instance._TcpClient.Client != null && Instance._TcpClient.Client.Connected)
                {
                    if (Instance._TcpClient.Client.Poll(0, SelectMode.SelectRead))// Detect if client disconnected
                    {
                        byte[] buff = new byte[1];
                        if (Instance._TcpClient.Client.Receive(buff, SocketFlags.Peek) == 0)
                            return false;
                        else
                            return true;
                    }
                    return true;
                }
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }
    }
    /// <returns> Buffer size for data exchange</returns>
    public static int BufferSize
    {
        get
        {
            if (Instance != null)
                return Instance._bufferSize;
            return -1; //ERROR
        }
        set
        {
            if (Instance != null)
            {
                Instance._bufferSize = value;
                Instance._TcpClient.ReceiveBufferSize = value; //setting receiving buffer size
                Instance._TcpClient.SendBufferSize = value; //setting sending buffer size
                Instance._buffer = new byte[value]; //allocating new buffer
            }
        }
    }
    /// <returns>  Last messages if there were any, else returns null </returns>
    public static List<byte[]> ReceiveMessages()
    {
        if(Instance!=null&& Instance._MessBox.Count>0)
        {
            List<byte[]> copy = Instance._MessBox;
            Instance._MessBox = new List<byte[]>();
            return copy;
        }
        return null;
    }
    /// <summary> Sends message to client/server</summary>
    /// <param name="message">message encoded in byte array</param>
    public static void SendMessage(byte[] message)
    {
        if(Instance!=null)
        {
            try
            {
                if (Instance._TcpClient != null)
                {
                    Instance._NetworkStream.BeginWrite(message, 0, message.Length, null, null);
                }
                else Debug.LogError(":( Error while sending data. Not connected!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError(":( Error while sending data. Error Code: " + ex);
            }
        }
    }

    private void _Stop()
    {
        try
        {
            Debug.Log("Stopping connection...");
            Instance._Role = ConnectionRole.Idle;
            if (Instance._listener != null)
                Instance._listener.Stop();
            if (Instance._TcpClient != null)
                _TcpClient.Close();
            if (Instance._NetworkStream != null)
                Instance._NetworkStream.Close();
        }
        catch (System.Exception ex)
        {
            Debug.LogError(":( Error while stopping the connection. Error Code: " + ex);
        }
    }
    public void _Server_Callback(System.IAsyncResult Result)
    {
        _TcpClient = _listener.EndAcceptTcpClient(Result);
        if (_TcpClient.Connected) // Checking if connected
        {
            _Role = ConnectionRole.Server;
            Debug.Log("Client " + _TcpClient.Client.RemoteEndPoint.AddressFamily.ToString() + " connected to the server");// Print ip
            _listener.BeginAcceptTcpClient(new System.AsyncCallback(_Server_Callback), null);
            _Start_Listening();
        }
    }
    public void _Client_Callback(System.IAsyncResult Result)
    {
        _TcpClient.EndConnect(Result);
        if (_TcpClient.Connected) // Checking if connected
        {
            Debug.Log("Connected to Server!");
            _Role = ConnectionRole.Client;
            _Start_Listening();
        }
        else
        {
            Debug.LogError(":( Error while connecting to server!");
            return;
        }
    }
    private void _Start_Listening()
    {
        _TcpClient.ReceiveBufferSize = _bufferSize; //setting receiving buffer size
        _TcpClient.SendBufferSize = _bufferSize; //setting sending buffer size
        _buffer = new byte[_bufferSize]; //allocating new buffer
        _NetworkStream = _TcpClient.GetStream();
        _NetworkStream.BeginRead(_buffer, 0, _bufferSize, new System.AsyncCallback(_Receive_Data), null); //Calling "Server_Receive_Data" when recieve data
    }
    private void _Receive_Data(System.IAsyncResult Result)
    {
        try
        {
            if(_NetworkStream!=null)
            {
                int ReceivedDataLength = _NetworkStream.EndRead(Result); //How many bytes was received
                if (ReceivedDataLength <= 0)
                {
                    Debug.LogWarning(":( Warrning - empty or broken data");
                    return;
                }
                _NetworkStream.BeginRead(_buffer, 0, _bufferSize, new System.AsyncCallback(_Receive_Data), null); //Listening for next data
                //Saving to message box
                _MessBox.Add(_buffer);
            }
        }
        catch (System.Exception ex)
        {
            if(!ex.Message.Contains("Cannot access a disposed object.")) //Error not to wory about
                Debug.LogError(":( Error while receiving data. Error Code: " + ex);
        }
    }


    private void OnDestroy()
    {
        //Stop Server
        _Stop();
    }
    private void OnApplicationQuit()
    {
        //Stop Server
        _Stop();
    }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else Destroy(this);
    }
}
