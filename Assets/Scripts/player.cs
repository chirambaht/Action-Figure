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

public class player : MonoBehaviour {
	// Start is called before the first frame update
	static int NUMBER_OF_DEVICES = 3;
	static int DATA_POINTS		 = 4;

	Vector3	   touchStart;
	static int DATA_START_POINT = 3;

	string path = "";

	static string[] combo_list = { "231001", "231010", "231011", "231100", "231101", "231110", "231111", "231000" };
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

	public Transform  groundCheckTransform;
	GameObject		  bone_upper;
	GameObject		  bone_lower;
	GameObject		  bone_hand;
	public GameObject side_camera;
	int				  max_cams = 0;
	Camera			  working_camera;

	Transform hand, bicep, forearm;

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
	string ip_choice;
	int	   mass_choice;

	// Network Variables
	public const int port	   = 9022;
	string			 server_ip = "192.168.1.102";
	Byte[] rec_data			   = new Byte[30];

	// TCP Variables
	TcpClient	  tcp_client = new TcpClient();
	Thread		  networkThread;
	NetworkStream tcp_stream;

	// Logging Variables
	StreamWriter log_writer;
	DateTime	 log_time;
	// COrrects the quaternions base on the MPU direction

	const float zoomOutMin = 3;
	const float zoomOutMax = 0;
	void		zoom( float increment ) {
			   if( working_camera.transform.position.z + increment < zoomOutMax * -1 || working_camera.transform.position.z + increment > -1 * zoomOutMin ) {
				   return;
		   }

			   working_camera.transform.position = working_camera.transform.position + new Vector3( 0, 0, increment );
	}
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

		for( int i = DATA_START_POINT; i < l; i++ ) {
			float_array[i] = BitConverter.ToInt16( byte_array, i * 2 ) / ( factor );
			if( float_array[i] > 1 ) {
				float_array[i] = 1;
			} else if( float_array[i] < -1 ) {
				float_array[i] = -1;
			}
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

		set_combo( combo_list[combo_index] );
	}

	public void set_combo( string in_combo = "231001" ) {
		string combo = in_combo;
		while( combo.Length < 6 ) {
			combo += "0";
		}
		if( combo.Length > 6 ) {
			combo = combo.Substring( 0, 6 );
		}

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
	}

	void Start() {
		max_cams	   = Camera.allCamerasCount;
		working_camera = Camera.allCameras[0];
		mainPlayer	   = GetComponent<Rigidbody>();
		mass_choice	   = PlayerPrefs.GetInt( "mass" );

		gender_choice = PlayerPrefs.GetString( "gender" );

		hand_choice = PlayerPrefs.GetString( "hand" );
		if( hand_choice == "Left" ) {
			bone_upper = GameObject.Find( "mixamorig:LeftArm" );
			bone_lower = GameObject.Find( "mixamorig:LeftForeArm" );
			bone_hand  = GameObject.Find( "mixamorig:LeftHand" );

			side_camera.transform.position = new Vector3( 1.25f, 2f, -0.5f );
			side_camera.transform.rotation = Quaternion.Euler( 0, -45f, 0 );
		} else {
			bone_upper = GameObject.Find( "mixamorig:RightArm" );
			bone_lower = GameObject.Find( "mixamorig:RightForeArm" );
			bone_hand  = GameObject.Find( "mixamorig:RightHand" );

			side_camera.transform.position = new Vector3( -1.25f, 2f, -0.5f );
			side_camera.transform.rotation = Quaternion.Euler( 0, 45f, 0 );
		}
		name_choice = PlayerPrefs.GetString( "name" );

		mainPlayer.mass = mass_choice;
		ip_choice		= PlayerPrefs.GetString( "ip" );
		server_ip		= ip_choice;
		bicep			= bone_upper.transform;
		hand			= bone_hand.transform;
		forearm			= bone_lower.transform;
		// All game objects to be assigned in the properties of the model.
		set_combo( "231001" );
		// udp_client = new UdpClient();
		Array.Clear( new_values, 0, 2 );
		Array.Clear( base_values, 0, 2 );
		Array.Clear( raw_values, 0, 2 );
		Array.Clear( placed_values, 0, 2 );
		Array.Clear( manipulated_values, 0, 2 );

		networkThread			   = new Thread( new ThreadStart( GetNetData ) );
		networkThread.IsBackground = true;

		// path = EditorUtility.SaveFolderPanel("Choose where to save your log.", "", "Action Traced");
		path = Application.persistentDataPath.ToString();
		if( path.Length == 0 ) {
			path = "";
		}
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

		int waited_data_messages = 0;
		log_time				 = DateTime.Now;
		Debug.LogFormat( "Logging to {2}/{0}_{1}.act", name_choice, log_time.ToString( "yyyyMMdd_HHmmss" ), path );
		string file_name_for_log = path + "/" + name_choice + "_" + log_time.ToString( "yyyyMMdd_HHmmss" ) + ".act";

		try {
			log_writer = new( file_name_for_log, append: true );

			log_writer.WriteLine( String.Format( "Name: {0}, Mass: {1}, Hand: {2}, Gender: {3}, IP: {4}", name_choice, mass_choice, hand_choice, gender_choice, ip_choice ) );
		} catch( Exception e ) {
			Debug.Log( e.Message );
		}

		while( true ) {
			try {
				Debug.LogFormat( "Connecting to: {0}:9022", server_ip );
				tcp_client = new TcpClient( server_ip, 9022 );
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
					float[] in_data = get_float_array_from_byte_array( rec_data );

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
			} catch( Exception err ) {
				err.ToString();
			} finally {
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

		float a2 = Quaternion.Angle( bicep.rotation, forearm.rotation );
		float a3 = Quaternion.Angle( forearm.rotation, hand.rotation );

		text_stats.text	 = String.Format( "Rotations\nBicep - Forearm\t{0}\nForearm - Hand\t{1}", a2, a3 );
		debug_stats.text = String.Format( "Received:\n{0}\nPlaced:\n{1}", float_array_to_string( raw_values ), float_array_to_string( new_values ) );
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

			if( networkThread.IsAlive ) {
				networkThread.Abort();
				Debug.Log( "Network thread stopped" );
			}
			Debug.Log( "Done" );
		} catch( Exception e ) {
			Debug.LogException( e, this );
		}
	}

	public void back_to_main_menu() {
		try {
			Debug.Log( "Closing everything..." );

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

			if( networkThread.IsAlive ) {
				networkThread.Abort();
				Debug.Log( "Network thread stopped" );
			}
			SceneManager.LoadScene( "Menu" );
		} catch( Exception e ) {
			Debug.LogException( e, this );
		}
	}
}
