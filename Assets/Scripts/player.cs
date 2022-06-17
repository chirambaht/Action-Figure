using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;

public class player : MonoBehaviour {
	// Start is called before the first frame update
	static int NUMBER_OF_DEVICES = 3;
	static int DATA_POINTS		 = 4;
	static int DATA_START_POINT	 = 3;

	static string[] combo_list = { "123001", "123010", "123011", "123100", "123101", "123110", "123111", "123000", "132001", "132010", "132011", "132100", "132101", "132110", "132111", "132000", "213001", "213010", "213011", "213100", "213101", "213110", "213111", "213000", "231001", "231010", "231011", "231100", "231101", "231110", "231111", "231000", "312001", "312010", "312011", "312100", "312101", "312110", "312111", "312000", "321001", "321010", "321011", "321100", "321101", "321110", "321111", "321000" };
	int combo_index			   = 0;

	public int DATA_PACKET_W = 0;
	public int DATA_PACKET_X = 2;
	public int DATA_PACKET_Y = 3;
	public int DATA_PACKET_Z = 1;

	public int W_SCALER = 1;
	public int X_SCALER = -1;
	public int Y_SCALER = -1;
	public int Z_SCALER = 1;

	public float rate = 0.1f;

	public TextMeshProUGUI text_stats;
	public TextMeshProUGUI debug_stats;

	public Transform groundCheckTransform;
	public Renderer	 handy_dandy;

	public GameObject bone_shoulder_right;
	public GameObject bone_upper_right;
	public GameObject bone_lower_right;
	public GameObject bone_hand_right;

	Transform shoulder, hand, bicep, forearm;

	float[,] new_values			= new float[NUMBER_OF_DEVICES, DATA_POINTS];
	float[,] base_values		= new float[NUMBER_OF_DEVICES, DATA_POINTS];
	float[,] raw_values			= new float[NUMBER_OF_DEVICES, DATA_POINTS];
	float[,] placed_values		= new float[NUMBER_OF_DEVICES, DATA_POINTS];
	float[,] manipulated_values = new float[NUMBER_OF_DEVICES, DATA_POINTS];

	float horizontalInput;
	float verticalInput;

	float[] offsets = new float[4];
	Rigidbody mainPlayer;

	// Stored variables
	string hand_choice;
	string gender_choice;
	string name_choice;
	int	   mass_choice;

	// Network Variables
	public const int	port  = 9022;
	public const string my_ip = "192.168.1.102";
	Byte[] rec_data			  = new Byte[30];

	// UDP Variables
	UdpClient udp_client;

	// TCP Variables
	TcpClient	  tcp_client;
	TcpListener	  tcp_listener;
	Thread		  networkThread;
	NetworkStream tcp_stream;

	// Logging Variables
	StreamWriter log_writer;
	DateTime	 log_time;
	// COrrects the quaternions base on the MPU direction
	public Quaternion quaternion_manipulator( Quaternion incoming_quaternion ) {
		Quaternion temp;
		temp.w = incoming_quaternion.w;
		temp.x = incoming_quaternion.y;
		temp.z = incoming_quaternion.z;
		temp.y = incoming_quaternion.x * -1;

		return temp.normalized;
	}

	public float[] quaternion_to_array( Quaternion incoming_quaternion ) {
		float[] outer = new float[DATA_POINTS];
		outer[0]	  = incoming_quaternion.w;
		outer[1]	  = incoming_quaternion.x;
		outer[2]	  = incoming_quaternion.y;
		outer[3]	  = incoming_quaternion.z;
		return outer;
	}

	private int get_int_from_byte( Byte b1, Byte b2 ) {
		return ( ( b2 << 8 ) + b1 );
	}

	public void log_packet( float[] packet ) {
		string log_string = "";
		for( int i = 0; i < packet.Length; i++ ) {
			log_string += packet[i].ToString() + "\t";
		}
		log_writer.WriteLine( log_string );
	}

	public float[] get_float_array_from_byte_array( Byte[] byte_array, int length = 30, float factor = 10000 ) {
		int l				= length / 2; // Number of integers in the array
		float[] float_array = new float[l];
		for( int i = 0; i < DATA_START_POINT; i++ ) {
			float_array[i] = BitConverter.ToInt16( byte_array, i * 2 );
		}

		for( int i = DATA_START_POINT; i < l; i++ ) {
			float_array[i] = BitConverter.ToInt16( byte_array, i * 2 ) / ( factor );
			// float_array[i] = get_int_from_byte( byte_array[i * 2], byte_array[i * ( 2 + 1 )] );
		}
		return float_array;
	}

	public float[,] quaternion_to_array( GameObject a, GameObject b, GameObject c ) {
		float[,] outer = new float[NUMBER_OF_DEVICES, DATA_POINTS];
		float[] data = new float[DATA_POINTS];
		for( int i = 0; i < NUMBER_OF_DEVICES; i++ ) {
			if( i == 0 ) {
				data = quaternion_to_array( a.transform.rotation );
			} else if( i == 1 ) {
				data = quaternion_to_array( b.transform.rotation );
			} else {
				data = quaternion_to_array( c.transform.rotation );
			}
			for( int j = 0; j < DATA_POINTS; j++ ) {
				outer[i, j] = data[j];
			}
		}
		return outer;
	}

	public string float_array_to_string( float[,] incoming_floats ) {
		string temp_string = "";
		for( int outer = 0; outer < NUMBER_OF_DEVICES; outer++ ) {
			for( int i = 0; i < DATA_POINTS; i++ ) {
				temp_string = temp_string + Math.Round( incoming_floats[outer, i], 3 ).ToString() +
					( ( i == DATA_POINTS - 1 ) ? "\n" : "    " );
			}
		}
		return temp_string;
	}

	public void t_pose() {
		// Recenters according to T-Pose
		for( int i = 0; i < NUMBER_OF_DEVICES; i++ ) {
			for( int j = 0; j < DATA_POINTS; j++ ) {
				base_values[i, j] = 0 - new_values[i, j];
			}
		}
	}

	public void next_combo( bool forward = true ) {
		if( forward ) {
			combo_index++;
		} else {
			combo_index--;
		}

		if( combo_index > combo_list.Length - 1 ) {
			combo_index = 0;
		} else if( combo_index < 0 ) {
			combo_index = combo_list.Length - 1;
		}

		string combo = combo_list[combo_index];
		Debug.LogFormat( "Combo: {0}", combo );

		int x = int.Parse( combo.Substring( 3, 1 ) );
		int y = int.Parse( combo.Substring( 4, 1 ) );
		int z = int.Parse( combo.Substring( 5, 1 ) );

		if( x == 0 ) {
			X_SCALER = -1;
		} else {
			X_SCALER = 1;
		}
		if( y == 0 ) {
			Y_SCALER = -1;
		} else {
			Y_SCALER = 1;
		}
		if( z == 0 ) {
			Z_SCALER = -1;
		} else {
			Z_SCALER = 1;
		}
		DATA_PACKET_X = int.Parse( combo.Substring( 0, 1 ) );
		DATA_PACKET_Y = int.Parse( combo.Substring( 1, 1 ) );
		DATA_PACKET_Z = int.Parse( combo.Substring( 2, 1 ) );
		// X_SCALER	  = x;
		// Y_SCALER	  = y;
		// Z_SCALER	  = z;
	}

	void Start() {
		mainPlayer = GetComponent<Rigidbody>();

		hand_choice	  = PlayerPrefs.GetString( "hand" );
		gender_choice = PlayerPrefs.GetString( "gender" );
		name_choice	  = PlayerPrefs.GetString( "name" );
		mass_choice	  = PlayerPrefs.GetInt( "mass" );

		Debug.LogFormat( "Name: {0}\nMass: {1}\nGender: {2}\nHand: {3}", name_choice, mass_choice, gender_choice, hand_choice );

		bicep = bone_upper_right.transform;
		// shoulder = bone_shoulder_right.transform;
		hand	= bone_hand_right.transform;
		forearm = bone_lower_right.transform;
		// All game objects to be assigned in the properties of the model.

		// udp_client = new UdpClient();
		Array.Clear( new_values, 0, 2 );
		Array.Clear( base_values, 0, 2 );
		Array.Clear( raw_values, 0, 2 );
		Array.Clear( placed_values, 0, 2 );
		Array.Clear( manipulated_values, 0, 2 );

		networkThread			   = new Thread( new ThreadStart( GetNetData ) );
		networkThread.IsBackground = true;
		networkThread.Start();
	}

	/** Returns a wrapped around float between -1 and 1.*/
	public float value_clamper( float incoming_number ) {
		float max = 1f, min = -1f, val = incoming_number;
		if( incoming_number >= max ) {
			float excess = incoming_number % 1;
			val			 = -1 + excess;
		} else if( incoming_number <= min ) {
			float excess = incoming_number % 1;
			val			 = excess;
		}

		return val;
	}

	void GetNetData() {
		// Initialize rec_data to 0
		Array.Clear( rec_data, 0, rec_data.Length );

		// UDP Variables
		// IPEndPoint me = new IPEndPoint(IPAddress.Parse("169.254.121.174"), port);
		// IPEndPoint me = new IPEndPoint(IPAddress.Parse(my_ip), port);
		// client = new UdpClient(me);
		// client.Client.Blocking = false;
		// client.Client.ReceiveTimeout = 100;

		// TCP Variables
		Debug.LogFormat( "Started network thread. Listening on: {0}:{1}", my_ip, port );

		tcp_listener = new TcpListener( IPAddress.Parse( my_ip ), port );
		tcp_listener.Start();
		int waited_data_messages = 0;
		log_time				 = DateTime.Now;
		Debug.LogFormat( "Logging to {0}.act", log_time.ToString( "yyyyMMdd_HHmmss" ) );
		string file_name_for_log = log_time.ToString( "yyyyMMdd_HHmmss" ) + ".act";
		try {
			log_writer = new( file_name_for_log, append: true );
		} catch( Exception e ) {
			Debug.Log( e.Message );
		}

		while( true ) {
			try {
				Debug.Log( "Waiting for a connection... " );
				tcp_client = tcp_listener.AcceptTcpClient();
				Debug.LogFormat( "Connected to client {0}", tcp_client.Client.RemoteEndPoint );

				tcp_stream = tcp_client.GetStream();

				while( tcp_client.Connected ) {
					if( !tcp_stream.DataAvailable ) {
						waited_data_messages++;
						if( waited_data_messages > 60 ) {
							waited_data_messages = 0;
							Debug.Log( "No data received from client." );
							break;
						}
						if( waited_data_messages < 30 ) {
							Thread.Sleep( 500 );
						} else {
							Thread.Sleep( 1000 );
							Debug.LogFormat( "Only {0}s left to for data", 60 - waited_data_messages );
						}
						continue;
					}

					int bbyytteess		 = tcp_stream.Read( rec_data, 0, rec_data.Length );
					waited_data_messages = 0;
					// print the received bytes
					// Debug.LogFormat( "I got {30} bytes:\n{0}-{1} {2}-{3} {4}-{5} || {6}-{7} {8}-{9} {10}-{11} {12}-{13} | {14}-{15} {16}-{17} {18}-{19} {20}-{21} | {22}-{23} {24}-{25} {26}-{27} {28}-{29}", rec_data[0], rec_data[1], rec_data[2], rec_data[3], rec_data[4], rec_data[5], rec_data[6], rec_data[7], rec_data[8], rec_data[9], rec_data[10], rec_data[11], rec_data[12], rec_data[13], rec_data[14], rec_data[15], rec_data[16], rec_data[17], rec_data[18], rec_data[19], rec_data[20], rec_data[21], rec_data[22], rec_data[23], rec_data[24], rec_data[25], rec_data[26], rec_data[27], rec_data[28], rec_data[29], bbyytteess );
					float[] in_data = get_float_array_from_byte_array( rec_data );
					// Debug.LogFormat( "Floats: {0} {1} {2} | {3} {4} {5} {6} | {7} {8} {9} {10} | {11} {12} {13} {14}", in_data[0], in_data[1], in_data[2], in_data[3], in_data[4], in_data[5], in_data[6], in_data[7], in_data[8], in_data[9], in_data[10], in_data[11], in_data[12], in_data[13], in_data[14] );

					// write packet to log file
					log_packet( in_data );

					for( int i = 0; i < NUMBER_OF_DEVICES; i++ ) {
						for( int j = 0; j < DATA_POINTS; j++ ) {
							raw_values[i, j] = in_data[( i * DATA_POINTS ) + j + DATA_START_POINT];
							new_values[i, j] = value_clamper( in_data[( i * DATA_POINTS ) + j + DATA_START_POINT] );
						}
					}

					// check if tcp client is still connected
					if( !tcp_client.Connected ) {
						Debug.Log( "Client disconnected." );
						break;
					}
				}

				// UDP Work
				// byte [] data = new byte[1024];

				// for (var a = 0; a < NUMBER_OF_DEVICES * DATA_POINTS; a++){
				//     data[a] = rec_data[DATA_START_POINT + a];
				// }

				// // encode UTF8-coded bytes to text format
				// string text = Encoding.UTF8.GetString(data);

				// string[] devices = text.Split(':');
				// float t;

				// for (var dev = 0; dev < NUMBER_OF_DEVICES; dev++)
				// {
				//     strin{
				//         t = float.Parse(single_device[val],
				//         System.Globalization.CultureInfo.InvariantCulture);
				//         raw_values[dev, val] = t;
				//         new_values[dev, val] = value_clamper(t);
				//     }
				// }g[] single_device = devices[dev].Split(',');

				//     for (var val = 0; val < DATA_POINTS; val++)
				//     {
				//         t = float.Parse(single_device[val],
				//         System.Globalization.CultureInfo.InvariantCulture);
				//         raw_values[dev, val] = t;
				//         new_values[dev, val] = value_clamper(t);
				//     }
				// }
			} catch( Exception err ) {
				err.ToString();
			} finally {
				// udp_client.Close();
				// Debug.Log( "UDP client closed" );

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
	void Update() {
		if( Input.GetKeyDown( KeyCode.R ) ) {
			// Print the rotation between forarm and bicep
		}
		if( Input.GetKeyDown( KeyCode.H ) ) {
			// Change combination to next one
			next_combo();
		}
		if( Input.GetKeyDown( KeyCode.G ) ) {
			// Change combination to previous one
			next_combo( false );
		}

		if( Input.GetKeyDown( KeyCode.T ) ) {
			t_pose();
		}

		for( int i = 0; i < NUMBER_OF_DEVICES; i++ ) {
			for( int j = 0; j < DATA_POINTS; j++ ) {
				placed_values[i, j] = base_values[i, j] + new_values[i, j];
			}
		}

		bicep.transform.rotation   = Quaternion.Lerp( bicep.transform.rotation, new Quaternion( placed_values[0, DATA_PACKET_X] * X_SCALER, placed_values[0, DATA_PACKET_Y] * Y_SCALER, placed_values[0, DATA_PACKET_Z] * Z_SCALER, placed_values[0, DATA_PACKET_W] * W_SCALER ), rate );
		forearm.transform.rotation = Quaternion.Lerp( forearm.transform.rotation, new Quaternion( placed_values[1, DATA_PACKET_X] * X_SCALER, placed_values[1, DATA_PACKET_Y] * Y_SCALER, placed_values[1, DATA_PACKET_Z] * Z_SCALER, placed_values[1, DATA_PACKET_W] * W_SCALER ), rate );
		hand.transform.rotation	   = Quaternion.Lerp( hand.transform.rotation, new Quaternion( placed_values[2, DATA_PACKET_X] * X_SCALER, placed_values[2, DATA_PACKET_Y] * Y_SCALER, placed_values[2, DATA_PACKET_Z] * Z_SCALER, placed_values[2, DATA_PACKET_W] * W_SCALER ), rate );

		// float a1 = Quaternion.Angle( shoulder.rotation, forearm.rotation );
		float a2 = Quaternion.Angle( bicep.rotation, forearm.rotation );
		float a3 = Quaternion.Angle( forearm.rotation, hand.rotation );

		manipulated_values = quaternion_to_array( bone_upper_right, bone_lower_right, bone_hand_right );

		text_stats.text =
			String.Format( "Rotations\nBicep - Forearm\t{0}\nForearm - Hand\t{1}", a2, a3 );
		debug_stats.text = String.Format(
			"Received:\n{0}\nBases:\n{1}\nTransform:\n{2}\nManipulated:\n{3}\nPlaced:\n{4}",
			float_array_to_string( raw_values ), float_array_to_string( base_values ), float_array_to_string( new_values ),
			float_array_to_string( manipulated_values ), float_array_to_string( placed_values ) );
	}

	void FixedUpdate() {
		if( Physics.OverlapSphere( groundCheckTransform.position, 0.1f ).Length <= 1 ) {
			return;
		}
	}

	void OnApplicationQuit() {
		try {
			Debug.Log( "Closing everything..." );

			// udp_client.Close();
			// Debug.Log( "UDP client closed" );

			if( log_writer != null ) {
				log_writer.Close();
				Debug.Log( "Log writer closed" );
			}

			if( tcp_stream != null ) {
				tcp_stream.Close();
				tcp_stream.Dispose();
				Debug.Log( "TCP stream closed" );
			}

			if( tcp_client.Connected ) {
				tcp_client.Close();
				Debug.Log( "TCP client closed" );
			}

			if( tcp_listener.Server.IsBound ) {
				tcp_listener.Stop();
				Debug.Log( "TCP listener stopped" );
			}

			if( networkThread.IsAlive ) {
				networkThread.Abort();
				Debug.Log( "Network thread stopped" );
			}
			Debug.Log( "Done" );
		} catch( Exception e ) {
			Debug.LogException( e, this );
		}
	}
}
