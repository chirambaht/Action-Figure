using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using ActionTracer;

public class player : MonoBehaviour
{

    bool disconnect = false;
    // Start is called before the first frame update
    // static int NUMBER_OF_DEVICES = 3;
    // static int DATA_POINTS = 4;

    Vector3 touchStart;

    uint last_packet = 0;

    string path = "";

    // static string[] combo_list = { "231001", "231010", "231011", "231100", "231101", "231110", "231111", "231000" };

    // public int DATA_PACKET_W = 0;
    // public int DATA_PACKET_X = 2;
    // public int DATA_PACKET_Y = 3;
    // public int DATA_PACKET_Z = 1;

    // public int W_SCALER = 1;
    // public int X_SCALER = -1;
    // public int Y_SCALER = -1;
    // public int Z_SCALER = 1;

    public float rate;

    float angle_wrist, angle_elbow;

    ActionDataNetworkPackage received_data_packet;
    ActionDataNetworkPackage.Types.ActionDeviceData data_hand, data_forearm, data_bicep;

    Quaternion q_hand = new Quaternion(0,0,0,0); 
    Quaternion q_forearm = new Quaternion(0,0,0,0); 
    Quaternion q_bicep = new Quaternion(0,0,0,0);

    Quaternion ref_bicep = Quaternion.identity;
    Quaternion ref_forearm = Quaternion.identity;
    Quaternion ref_hand = Quaternion.identity; 

    public TextMeshProUGUI text_stats;
    public TextMeshProUGUI debug_stats;

    public Transform groundCheckTransform;
    GameObject bone_upper;
    GameObject bone_lower;
    GameObject bone_hand;
    public GameObject side_camera;
    int max_cams = 0;
    Camera working_camera;

    Transform hand, bicep, forearm;

    float horizontalInput;
    float verticalInput;

    float[] offsets = new float[4];
    Rigidbody mainPlayer;

    // Stored variables
    string hand_choice;
    string gender_choice;
    string name_choice;
    string ip_choice;
    int mass_choice;

    // Network Variables
    public const int port = 9022;
    string server_ip = "192.168.1.102";
    Byte[] rec_data_len = new Byte[4];

    // TCP Variables
    TcpClient tcp_client = new TcpClient();
    Thread networkThread;
    NetworkStream tcp_stream;

    // Logging Variables
    StreamWriter log_writer;
    DateTime log_time;
    // COrrects the quaternions base on the MPU direction

    const float zoomOutMin = 3;
    const float zoomOutMax = 0;

    void zoom(float increment)
    {
        if (working_camera.transform.position.z + increment < zoomOutMax * -1 || working_camera.transform.position.z + increment > -1 * zoomOutMin)
        {
            return;
        }

        working_camera.transform.position = working_camera.transform.position + new Vector3(0, 0, increment);
    }


    public void log_packet()
    {
        log_writer.WriteLine(received_data_packet.ToString());
    }


    public bool get_float_array_from_proto(Byte[] byte_array)
    {
        if (byte_array.Length == 0 || byte_array == null)
        {
            return false;
        }

        try
        {
            ActionMessage message = ActionMessage.Parser.ParseFrom(byte_array);

            if (message.Action == ActionCommand.Data)
            {
                received_data_packet = message.Data;

                if (received_data_packet.PacketNumber > last_packet){
                    last_packet = received_data_packet.PacketNumber;
                    return true;
                }
                return false;
            }
            else if (message.Action == ActionCommand.Disconnect)
            {
                Debug.Log("Disconnecting");
                disconnect = true;
                return true;
            }
        }
        catch (Exception e)
        {
            e.ToString();
            return false;
        }

        return false;
    }

    public void allocate_devices()
    {
        for (int i = 0; i < received_data_packet.DeviceData.Count; i++)
        {
            // You may need to swap the X and W values
            if (received_data_packet.DeviceData[i].DeviceIdentifierContents == 8)
            {
                data_bicep = received_data_packet.DeviceData[i].Clone();
                q_bicep = new Quaternion(data_bicep.Quaternion.W, data_bicep.Quaternion.Y, data_bicep.Quaternion.Z, data_bicep.Quaternion.X);
            }
            else if (received_data_packet.DeviceData[i].DeviceIdentifierContents == 16)
            {
                data_forearm = received_data_packet.DeviceData[i].Clone();
                q_forearm = new Quaternion(data_forearm.Quaternion.W, data_forearm.Quaternion.Y, data_forearm.Quaternion.Z, data_forearm.Quaternion.X);
            }
            else if (received_data_packet.DeviceData[i].DeviceIdentifierContents == 32)
            {
                data_hand = received_data_packet.DeviceData[i].Clone();
                q_hand = new Quaternion(data_hand.Quaternion.W, data_hand.Quaternion.Y, data_hand.Quaternion.Z, data_hand.Quaternion.X);
            }
        }

        q_bicep = Quaternion.Inverse(ref_bicep) * q_bicep;
        q_forearm = Quaternion.Inverse(ref_forearm) * q_forearm;
        q_hand = Quaternion.Inverse(ref_hand) * q_hand;

        // Subtract the initial rotation
        q_hand = Quaternion.Inverse(q_forearm) * q_hand;
        q_forearm = Quaternion.Inverse(q_bicep) * q_forearm;
        }


    public void t_pose()
    {
        ref_bicep = bicep.rotation;
        ref_forearm = forearm.rotation;
        ref_hand = hand.rotation;
    }

    public String parse_debug_stats(){

        String debug_line = "Received:\n";
        debug_line += "         " + "  w  " + "  " + "  x  " + "  " + "  y  " + "  " + "  z  " + "\n";
        debug_line += "  Bicep  " + q_bicep.w.ToString("0.000") + "  " + q_bicep.y.ToString("0.000") + "  " + q_bicep.z.ToString("0.000") + "  " + q_bicep.x.ToString("0.000") + "\n";
        debug_line += "Forearm  " + q_forearm.w.ToString("0.000") + "  " + q_forearm.y.ToString("0.000") + "  " + q_forearm.z.ToString("0.000") + "  " + q_forearm.x.ToString("0.000") + "\n";
        debug_line += "   Hand  " + q_hand.w.ToString("0.000") + "  " + q_hand.y.ToString("0.000") + "  " + q_hand.z.ToString("0.000") + "  " + q_hand.x.ToString("0.000") + "\n";
    
        return debug_line;
    }

    public void end_session()
    {
        try
        {
            Debug.Log("Closing everything...");

            // udp_client.Close();
            // Debug.Log( "UDP client closed" );

            if (log_writer != null)
            {
                log_writer.Close();
                Debug.Log("Log writer closed");
            }

            if (tcp_stream != null)
            {
                tcp_stream.Close();
                tcp_stream.Dispose();
                Debug.Log("TCP stream closed");
            }

            if (tcp_client.Connected)
            {
                tcp_client.Close();
                Debug.Log("TCP client closed");
            }

            if (networkThread.IsAlive)
            {
                networkThread.Abort();
                Debug.Log("Network thread stopped");
            }
            Debug.Log("Done");
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    void Start()
    {
        // Setup the simulation
        rate =1- Time.deltaTime;
        max_cams = Camera.allCamerasCount;
        working_camera = Camera.allCameras[0];
        mainPlayer = GetComponent<Rigidbody>();
        mass_choice = PlayerPrefs.GetInt("mass");

        gender_choice = PlayerPrefs.GetString("gender");

        hand_choice = PlayerPrefs.GetString("hand");
        if (hand_choice == "Left")
        {
            bone_upper = GameObject.Find("mixamorig:LeftArm");
            bone_lower = GameObject.Find("mixamorig:LeftForeArm");
            bone_hand = GameObject.Find("mixamorig:LeftHand");

            side_camera.transform.position = new Vector3(1.25f, 2f, -0.5f);
            side_camera.transform.rotation = Quaternion.Euler(0, -45f, 0);
        }
        else
        {
            bone_upper = GameObject.Find("mixamorig:RightArm");
            bone_lower = GameObject.Find("mixamorig:RightForeArm");
            bone_hand = GameObject.Find("mixamorig:RightHand");

            side_camera.transform.position = new Vector3(-1.25f, 2f, -0.5f);
            side_camera.transform.rotation = Quaternion.Euler(0, 45f, 0);
        }
        name_choice = PlayerPrefs.GetString("name");

        

        mainPlayer.mass = mass_choice;
        ip_choice = PlayerPrefs.GetString("ip");
        server_ip = ip_choice;

        bicep = bone_upper.transform;
        hand = bone_hand.transform;
        forearm = bone_lower.transform;

        networkThread = new Thread(new ThreadStart(GetNetData));
        networkThread.IsBackground = true;

        path = Application.persistentDataPath.ToString();
        if (path.Length == 0)
        {
            path = "";
        }
        networkThread.Start();
    }

    void GetNetData()
    {
        int waited_data_messages = 0;
        log_time = DateTime.Now;
        Debug.LogFormat("Logging to {2}/{0}_{1}.act", name_choice, log_time.ToString("yyyyMMdd_HHmmss"), path);
        string file_name_for_log = path + "/" + name_choice + "_" + log_time.ToString("yyyyMMdd_HHmmss") + ".act";

        try
        {
            log_writer = new(file_name_for_log, append: true);

            log_writer.WriteLine(String.Format("Name: {0}, Mass: {1}, Hand: {2}, Gender: {3}, IP: {4}", name_choice, mass_choice, hand_choice, gender_choice, ip_choice));
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }

        while (true)
        {
            try
            {
                while (disconnect)
                {
                    Debug.Log("Disconnected from client. Press escape to exit.");
                    Thread.Sleep(30000);
                    // end_session();

                    // Show popup eventually

                }

                Debug.LogFormat("Connecting to: {0}:9022", server_ip);
                tcp_client = new TcpClient(server_ip, 9022);
                Debug.LogFormat("Connected to client {0}", tcp_client.Client.RemoteEndPoint);

                tcp_stream = tcp_client.GetStream();

                while (tcp_client.Connected)
                {
                    if (!tcp_stream.DataAvailable)
                    {
                        waited_data_messages++;
                        if (waited_data_messages > 60)
                        {
                            waited_data_messages = 0;
                            Debug.Log("No data received from client.");
                            break;
                        }
                        if (waited_data_messages < 50)
                        {
                            Thread.Sleep(500);
                        }
                        else
                        {
                            Thread.Sleep(1000);
                            Debug.LogFormat("Only {0}s left to for data", 60 - waited_data_messages);
                        }
                        continue;
                    }
                    // Read the length of the packet
                    int bbyytteess = tcp_stream.Read(rec_data_len, 0, 4);
                    int l = BitConverter.ToInt32(rec_data_len, 0);

                    // Read the data packet
                    Byte[] proto_rec_data = new Byte[l];
                    bbyytteess = tcp_stream.Read(proto_rec_data, 0, proto_rec_data.Length);

                    // print the received bytes
                    bool data_in = get_float_array_from_proto(proto_rec_data);

                    // Debug.LogFormat("Received {0} bytes and packet status is: {1}", bbyytteess, data_in);

                    if (data_in && disconnect)
                    {
                        Debug.Log("Server has requested a disconnect.");
                        break;
                    }
                    else if (!data_in)
                    {
                        Debug.Log("Bad packet");
                    }

                    waited_data_messages = 0;
                    allocate_devices();
                    log_packet();

                    // check if tcp client is still connected
                    if (!tcp_client.Connected)
                    {
                        Debug.Log("Client disconnected.");
                        break;
                    }
                }
            }
            catch (Exception err)
            {
                err.ToString();
            }
            finally
            {
                // if( tcp_stream != null ) {
                // 	tcp_stream.Close();
                // 	tcp_stream.Dispose();
                // 	Debug.Log( "TCP stream closed" );
                // }

                // if( tcp_client.Connected ) {
                // 	tcp_client.Close();
                // 	Debug.Log( "TCP client closed" );
                // }
            }
        }
    }

    

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey("escape"))
        {
            end_session();
            SceneManager.LoadScene("Menu");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            // Print the rotation between forarm and bicep
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            // Change combination to next one
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            // Change combination to previous one
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            t_pose();
        }


        try
        {
            bicep.transform.rotation = Quaternion.Lerp(bicep.transform.rotation, q_bicep, rate);
            forearm.transform.rotation = Quaternion.Lerp(forearm.transform.rotation, q_forearm, rate);
            hand.transform.rotation = Quaternion.Lerp(hand.transform.rotation, q_hand, rate);
        }
        catch (Exception err)
        {
            err.ToString();
        }

        angle_elbow = 180 - Quaternion.Angle(bicep.rotation, forearm.rotation);
        angle_wrist = 180 - Quaternion.Angle(forearm.rotation, hand.rotation);

        text_stats.text = String.Format("Rotations\nBicep - Forearm\t{0}\nForearm - Hand\t{1}", angle_elbow, angle_wrist);
        debug_stats.text = parse_debug_stats();
    }

    void FixedUpdate()
    {
        if (Physics.OverlapSphere(groundCheckTransform.position, 0.1f).Length <= 1)
        {
            return;
        }
    }

    void OnApplicationQuit()
    {
        end_session();
    }

    public void back_to_main_menu()
    {
        try
        {
            Debug.Log("Closing everything...");

            if (log_writer != null)
            {
                log_writer.Close();
                Debug.Log("Log writer closed");
            }

            if (tcp_stream != null)
            {
                tcp_stream.Close();
                tcp_stream.Dispose();
                Debug.Log("TCP stream closed");
            }

            if (tcp_client.Connected)
            {
                tcp_client.Close();
                Debug.Log("TCP client closed");
            }

            if (networkThread.IsAlive)
            {
                networkThread.Abort();
                Debug.Log("Network thread stopped");
            }
            SceneManager.LoadScene("Menu");
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }
}
