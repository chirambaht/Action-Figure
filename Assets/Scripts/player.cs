using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;
using System.Globalization;

public class player : MonoBehaviour
{
    // Start is called before the first frame update
    static int NUMBER_OF_DEVICES = 3;
    static int DATA_POINTS = 4;

    public Transform groundCheckTransform;

    public GameObject bone_upper_right;
    public GameObject bone_lower_right;
    public GameObject bone_hand_right;

    float[,] new_values = new float[NUMBER_OF_DEVICES, DATA_POINTS];
    float[,] base_values = new float[NUMBER_OF_DEVICES, DATA_POINTS];

    float horizontalInput;
    float verticalInput;

    float[] offsets = new float[4];
    Rigidbody mainPlayer;

    public const int port = 9022;
    UdpClient client;
    Thread networkThread;

    // COrrects the quaternions base on the MPU direction
    public Quaternion quaternion_manipulator(Quaternion incoming_quaternion){
        Quaternion temp;
        temp.w = incoming_quaternion.w;
        temp.x = incoming_quaternion.y;
        temp.y = incoming_quaternion.z;
        temp.z = incoming_quaternion.x * -1;

        return temp;
    }

    public float[] quaternion_to_array(Quaternion incoming_quaternion){
        float[] outer = new float [4];
        outer[0] = incoming_quaternion.w;
        outer[1] = incoming_quaternion.x;
        outer[2] = incoming_quaternion.y;
        outer[3] = incoming_quaternion.z;
        return outer;
    }

    public void t_pose(){
        // Recenters according to T-Pose
        for (int i = 0; i < NUMBER_OF_DEVICES; i++){
            for (int j = 0; j < DATA_POINTS; j++){
                base_values[i,j] = 0 - new_values[i,j];
            }
        }
    }

    void Start()
    {
        mainPlayer = GetComponent<Rigidbody>();
        // All game objects to be assigned in the properties of the model.

        client = new UdpClient();
        Array.Clear(new_values, 0, 2);
        Array.Clear(base_values, 0, 2);

        // Collect T-Pose base values
        float[] data = new float[4];
        for (int i = 0; i < NUMBER_OF_DEVICES; i++){
            if (i == 0){
                data = quaternion_to_array(bone_upper_right.transform.rotation);
            }
            if (i == 1){
                data = quaternion_to_array(bone_lower_right.transform.rotation);
            }
            if (i == 2){
                data = quaternion_to_array(bone_hand_right.transform.rotation);
            }
            for (int j = 0; j < DATA_POINTS; j++){
                base_values[i,j] = data[j];
                Debug.Log(data[j]);
            }
        }

        // Debug.Log(String.Join(" ", base_values.Cast<float>()));

        networkThread = new Thread(new ThreadStart(GetNetData));
        networkThread.IsBackground = true;
        networkThread.Start();
    }

    /** Returns a wrapped around float between -1 and 1.*/
    public  float value_clamper(float incoming_number)
	{
		float max = 1f, min = -1f, val = incoming_number;
		if (incoming_number >= max){
			float excess = incoming_number % 1;
			val = -1 + excess;
			
		} else if (incoming_number <= min){
			float excess = incoming_number % 1;
			val = excess;
		}		

        return val;
		
	}



    void GetNetData()
    {
        // IPEndPoint me = new IPEndPoint(IPAddress.Parse("192.168.0.149"), port);
        IPEndPoint me = new IPEndPoint(IPAddress.Parse("169.254.121.174"), port);
        client = new UdpClient(me);
        client.Client.Blocking = false;
        client.Client.ReceiveTimeout = 100;

        Debug.Log("Started network thread. Listening on: " + client.Client.LocalEndPoint.ToString());

        while (true)
        {
            try
            {
                // receive bytes
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 9022);
                byte[] data = client.Receive(ref anyIP);

                // encode UTF8-coded bytes to text format
                string text = Encoding.UTF8.GetString(data);
                
                string[] devices = text.Split(':');
                float t;

                for (var dev = 0; dev < NUMBER_OF_DEVICES; dev++)
                {
                    string[] single_device = devices[dev].Split(',');

                    for (var val = 0; val < DATA_POINTS; val++)
                    {
                        t = float.Parse(single_device[val], System.Globalization.CultureInfo.InvariantCulture);
                        new_values[dev, val] = value_clamper(t);
                    }
                }
            }
            catch (Exception err)
            {
                err.ToString();
            }
        }
    }


    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)){
            // Print the rotation between forarm and bicep

            Debug.Log( Quaternion.Angle(bone_upper_right.transform.rotation, bone_lower_right.transform.rotation));
        }

        if (Input.GetKeyDown(KeyCode.T)){
            t_pose();
        }

        bone_upper_right.transform.rotation = quaternion_manipulator(new Quaternion(value_clamper(base_values[0, 1] + new_values[0, 1]) , value_clamper(base_values[0, 2] + new_values[0, 2]) , value_clamper(base_values[0, 3] + new_values[0, 3])  , value_clamper(base_values[0, 0] + new_values[0, 0]) ));
        bone_lower_right.transform.rotation = quaternion_manipulator(new Quaternion(value_clamper(base_values[1, 1] + new_values[1, 1]) , value_clamper(base_values[1, 2] + new_values[1, 2]) , value_clamper(base_values[1, 3] + new_values[1, 3])  , value_clamper(base_values[1, 0] + new_values[1, 0]) ));
        bone_hand_right.transform.rotation  = quaternion_manipulator(new Quaternion(value_clamper(base_values[2, 1] + new_values[2, 1]) , value_clamper(base_values[2, 2] + new_values[2, 2]) , value_clamper(base_values[2, 3] + new_values[2, 3])  , value_clamper(base_values[2, 0] + new_values[2, 0]) ));

    }

    void FixedUpdate()
    {
        if (Physics.OverlapSphere(groundCheckTransform.position, 0.1f).Length <= 1)
        {
            return;
        }

    }

    private void onTriggerEnter(Collision other)
    {
        Debug.Log("Crash");
        if (other.gameObject.name == "coin")
        {
            Destroy(other.gameObject);
        }
    }


    void OnApplicationQuit()
{
    try
    {
        client.Close();
    }
    catch(Exception e)
    {
        Debug.Log(e.Message);
    }
    }

}

