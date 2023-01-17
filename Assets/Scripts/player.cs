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

public class player : MonoBehaviour {
	bool disconnect = false;

	Vector3 touchStart;

	uint bad_packet_counter = 0;

	uint last_packet = 0;

	string path = "";

	// static string[] combo_list = { "231001", "231010", "231011", "231100", "231101", "231110", "231111", "231000" };.
	static string[] quat_combos = { "0000", "0001", "0010", "0011", "0100", "0101", "0110", "0111", "1000", "1001", "1010", "1011", "1100", "1101", "1110", "1111" };
	int quat_index				= 0;

	static string[] quat_order = { "wxyz", "wxzy", "wyxz", "wyzx", "wzxy", "wzyx", "xwyz", "xwzy", "xywz", "xyzw", "xzwy", "xzyw", "ywxz", "ywzx", "yxwz", "yxzw", "yzwx", "yzxw", "zwxy", "zwyx", "zxwy", "zxyw", "zywx", "zyxw" };
	int order_index			   = 0;

	public float rate;

	float angle_wrist, angle_elbow;

	ActionDataNetworkPackage						received_data_packet;
	ActionDataNetworkPackage.Types.ActionDeviceData data_hand, data_forearm, data_bicep;

	Quaternion q_hand	 = new Quaternion( 0, 0, 0, 0 );
	Quaternion q_forearm = new Quaternion( 0, 0, 0, 0 );
	Quaternion q_bicep	 = new Quaternion( 0, 0, 0, 0 );

	Quaternion ref_bicep   = Quaternion.identity;
	Quaternion ref_forearm = Quaternion.identity;
	Quaternion ref_hand	   = Quaternion.identity;

	public TextMeshProUGUI text_stats;
	public TextMeshProUGUI debug_stats;

	public Transform  groundCheckTransform;
	GameObject		  bone_upper;
	GameObject		  bone_lower;
	GameObject		  bone_hand;
	GameObject		  bone_finger;
	public GameObject side_camera;
	int				  max_cams = 0;
	Camera			  working_camera;

	Transform hand, bicep, forearm, finger;

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
	Byte[] rec_data_len		   = new Byte[4];

	// TCP Variables
	TcpClient	  tcp_client = new TcpClient();
	NetworkStream tcp_stream;
	Thread		  network_thread;

	// Logging Variables
	StreamWriter log_writer;
	StreamWriter data_writer;
	StreamWriter sol_writer;

	DateTime log_time;

	string file_name_for_log;
	string file_name_for_sol;
	string file_name_for_data;

	int file_number = 0;
	// COrrects the quaternions base on the MPU direction
	public AudioSource audioSource;

	const float zoomOutMin		 = 3;
	const float zoomOutMax		 = 0;
	bool		logging			 = false;
	bool		first_data_point = true;
	public void play_sound() {
		if( logging ) {
			return;
		}
		audioSource.Play();
		logging = true;
	}
	void zoom( float increment ) {
		if( working_camera.transform.position.z + increment < zoomOutMax * -1 || working_camera.transform.position.z + increment > -1 * zoomOutMin ) {
			return;
		}

		working_camera.transform.position = working_camera.transform.position + new Vector3( 0, 0, increment );
	}

	public void log_packet() {
		if( !logging ) {
			return;
		} else {
			if( first_data_point ) {
				try {
					path = Application.persistentDataPath.ToString();

					if( path.Length == 0 ) {
						path = "";
					}

					Debug.LogFormat( "Logging to {2}/{0}_{1}.act", name_choice, log_time.ToString( "yyyyMMdd_HHmmss" ), path );

					file_name_for_log = path + "/" + log_time.ToString( "yyyyMMdd_HHmmss" ) + "_" + name_choice + "_" + file_number.ToString() + ".act";

					file_name_for_sol  = log_time.ToString( "yyyyMMdd_HHmmss" ) + "_" + name_choice + "_" + file_number.ToString() + ".csv";
					file_name_for_data = log_time.ToString( "yyyyMMdd_HHmmss" ) + "_" + name_choice + "_" + file_number.ToString() + "_packets.csv";

					file_number++;

					log_writer	= new( file_name_for_log, append: true );
					sol_writer	= new( file_name_for_sol, append: true );
					data_writer = new( file_name_for_data, append: true );

					log_writer.WriteLine( String.Format( "Name: {0}, Mass: {1}, Hand: {2}, Gender: {3}, IP: {4}", name_choice, mass_choice, hand_choice, gender_choice, ip_choice ) );
					sol_writer.WriteLine( "time(s),upper_rot_x,upper_rot_y,upper_rot_z,lower_rot_x,lower_rot_y,lower_rot_z,hand_rot_x,hand_rot_y,hand_rot_z,finger_rot_x,finger_rot_y,finger_rot_z,upper_pos_x,upper_pos_y,upper_pos_z,lower_pos_x,lower_pos_y,lower_pos_z,hand_pos_x,hand_pos_y,hand_pos_z,finger_pos_x,finger_pos_y,finger_pos_z" );
					data_writer.WriteLine( "time(s),packet,id_1,quat_w_1,quat_x_1,quat_y_1,quat_z_1,accel_x_1,accel_y_1,accel_z_1,gyro_x_1,gyro_y_1,gyro_z_1,temp_1,id_2,quat_w_2,quat_x_2,quat_y_2,quat_z_2,accel_x_2,accel_y_2,accel_z_2,gyro_x_2,gyro_y_2,gyro_z_2,temp_2,id_3,quat_w_3,quat_x_3,quat_y_3,quat_z_3,accel_x_3,accel_y_3,accel_z_3,gyro_x_3,gyro_y_3,gyro_z_3,temp_3" );

				} catch( Exception e ) {
					Debug.Log( e.Message );
				}
				first_data_point = false;
			}
		}
		log_writer.WriteLine( received_data_packet.ToString() );
		TimeSpan t_span = DateTime.Now - log_time;

		string log_string;
		string data_string = "";

		// Add the current frame time
		log_string = ( t_span.TotalMilliseconds / 1000f ).ToString( "0.0000", CultureInfo.InvariantCulture ) + ",";
		// Add the rotations
		log_string += bicep.transform.eulerAngles.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + bicep.transform.eulerAngles.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + bicep.transform.eulerAngles.z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
		log_string += forearm.transform.eulerAngles.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + forearm.transform.eulerAngles.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + forearm.transform.eulerAngles.z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
		log_string += hand.transform.eulerAngles.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + hand.transform.eulerAngles.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + hand.transform.eulerAngles.z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
		log_string += finger.transform.eulerAngles.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + finger.transform.eulerAngles.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + finger.transform.eulerAngles.z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";

		// Add the positions
		log_string += bicep.transform.position.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + bicep.transform.position.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + bicep.transform.position.z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
		log_string += forearm.transform.position.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + forearm.transform.position.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + forearm.transform.position.z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
		log_string += hand.transform.position.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + hand.transform.position.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + hand.transform.position.z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
		log_string += finger.transform.position.x.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + finger.transform.position.y.ToString( "0.00000", CultureInfo.InvariantCulture ) + "," + finger.transform.position.z.ToString( "0.00000", CultureInfo.InvariantCulture );

		data_string = ( t_span.TotalMilliseconds / 1000f ).ToString( "0.0000", CultureInfo.InvariantCulture ) + ",";
		data_string += received_data_packet.PacketNumber.ToString() + ",";

		for( int i = 0; i < received_data_packet.DeviceData.Count; i++ ) {
			data_string += received_data_packet.DeviceData[i].DeviceIdentifierContents.ToString() + ",";

			data_string += received_data_packet.DeviceData[i].Quaternion.W.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
			data_string += received_data_packet.DeviceData[i].Quaternion.X.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
			data_string += received_data_packet.DeviceData[i].Quaternion.Y.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
			data_string += received_data_packet.DeviceData[i].Quaternion.Z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";

			data_string += received_data_packet.DeviceData[i].Accelerometer.X.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
			data_string += received_data_packet.DeviceData[i].Accelerometer.Y.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
			data_string += received_data_packet.DeviceData[i].Accelerometer.Z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";

			data_string += received_data_packet.DeviceData[i].Gyroscope.X.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
			data_string += received_data_packet.DeviceData[i].Gyroscope.Y.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";
			data_string += received_data_packet.DeviceData[i].Gyroscope.Z.ToString( "0.00000", CultureInfo.InvariantCulture ) + ",";

			data_string += received_data_packet.DeviceData[i].Temperature.ToString( "0.00000", CultureInfo.InvariantCulture );

			if( i < received_data_packet.DeviceData.Count - 1 ) {
				data_string += ",";
			}
		}

		sol_writer.WriteLine( log_string );
		data_writer.WriteLine( data_string );
	}

	public bool get_float_array_from_proto( Byte[] byte_array ) {
		if( byte_array.Length == 0 || byte_array == null ) {
			return false;
		}

		try {
			ActionMessage message = ActionMessage.Parser.ParseFrom( byte_array );

			if( message.Action == ActionCommand.Data ) {
				received_data_packet = message.Data;

				if( received_data_packet.PacketNumber > last_packet ) {
					last_packet = received_data_packet.PacketNumber;
					return true;
				}
				return false;
			} else if( message.Action == ActionCommand.Disconnect ) {
				Debug.Log( "Disconnecting" );
				disconnect = true;
				return true;
			}
		} catch( Exception e ) {
			e.ToString();
			return false;
		}

		return false;
	}

	Quaternion set_combo_quats( Quaternion inc, int combo, int order ) {
		float[] quat = { 0, 0, 0, 0 };

		int q_x_m, q_y_m, q_z_m, q_w_m;

		q_x_m = ( quat_combos[combo].Substring( 0, 1 ) == "0" ) ? -1 : 1;
		q_y_m = ( quat_combos[combo].Substring( 1, 1 ) == "0" ) ? -1 : 1;
		q_z_m = ( quat_combos[combo].Substring( 2, 1 ) == "0" ) ? -1 : 1;
		q_w_m = ( quat_combos[combo].Substring( 3, 1 ) == "0" ) ? -1 : 1;

		for( int i = 0; i < 4; i++ ) {
			switch( quat_order[order].Substring( i, 1 ) ) {
				case "w":
					quat[i] = inc.w * q_w_m;
					break;
				case "x":
					quat[i] = inc.x * q_x_m;
					break;
				case "y":
					quat[i] = inc.y * q_y_m;
					break;
				case "z":
					quat[i] = inc.z * q_z_m;
					break;
				default:
					quat[i] = 0;
					break;
			}
		}
		return new Quaternion( quat[0], quat[1], quat[2], quat[3] );
	}

	Quaternion get_combo_quats( Quaternion inc ) {
		float[] quat = { 0, 0, 0, 0 };

		int q_x_m, q_y_m, q_z_m, q_w_m;

		q_x_m = ( quat_combos[quat_index].Substring( 0, 1 ) == "0" ) ? -1 : 1;
		q_y_m = ( quat_combos[quat_index].Substring( 1, 1 ) == "0" ) ? -1 : 1;
		q_z_m = ( quat_combos[quat_index].Substring( 2, 1 ) == "0" ) ? -1 : 1;
		q_w_m = ( quat_combos[quat_index].Substring( 3, 1 ) == "0" ) ? -1 : 1;

		for( int i = 0; i < 4; i++ ) {
			switch( quat_order[order_index].Substring( i, 1 ) ) {
				case "w":
					quat[i] = inc.w * q_w_m;
					break;
				case "x":
					quat[i] = inc.x * q_x_m;
					break;
				case "y":
					quat[i] = inc.y * q_y_m;
					break;
				case "z":
					quat[i] = inc.z * q_z_m;
					break;
				default:
					quat[i] = 0;
					break;
			}
		}
		return new Quaternion( quat[0], quat[1], quat[2], quat[3] );
	}

	public void allocate_devices() {
		for( int i = 0; i < received_data_packet.DeviceData.Count; i++ ) {
			// You may need to swap the X and W values
			if( received_data_packet.DeviceData[i].DeviceIdentifierContents == 8 ) {
				data_bicep = received_data_packet.DeviceData[i].Clone();

				q_bicep = Quaternion.Inverse( ref_bicep ) * set_combo_quats( new Quaternion( data_bicep.Quaternion.W, data_bicep.Quaternion.X, data_bicep.Quaternion.Y, data_bicep.Quaternion.Z ), 6, 15 );

				// q_bicep = new Quaternion(data_bicep.Quaternion.X * q_x_m, data_bicep.Quaternion.Y * q_y_m, data_bicep.Quaternion.Z * q_z_m, data_bicep.Quaternion.W * q_w_m);
			} else if( received_data_packet.DeviceData[i].DeviceIdentifierContents == 16 ) {
				data_forearm = received_data_packet.DeviceData[i].Clone();

				// q_forearm = get_combo_quats( new Quaternion( data_forearm.Quaternion.W, data_forearm.Quaternion.X, data_forearm.Quaternion.Y, data_forearm.Quaternion.Z ) );
				q_forearm = Quaternion.Inverse( ref_forearm ) * set_combo_quats( new Quaternion( data_forearm.Quaternion.W, data_forearm.Quaternion.X, data_forearm.Quaternion.Y, data_forearm.Quaternion.Z ), 6, 15 );
				// q_forearm = new Quaternion(data_forearm.Quaternion.X, data_forearm.Quaternion.Y, data_forearm.Quaternion.Z, data_forearm.Quaternion.W);
			} else if( received_data_packet.DeviceData[i].DeviceIdentifierContents == 32 ) {
				data_hand = received_data_packet.DeviceData[i].Clone();

				// q_hand = get_combo_quats( new Quaternion( data_hand.Quaternion.W, data_hand.Quaternion.X, data_hand.Quaternion.Y, data_hand.Quaternion.Z ) );
				q_hand = Quaternion.Inverse( ref_hand ) * set_combo_quats( new Quaternion( data_hand.Quaternion.W, data_hand.Quaternion.X, data_hand.Quaternion.Y, data_hand.Quaternion.Z ), 6, 15 );
				// q_hand = new Quaternion(data_hand.Quaternion.X, data_hand.Quaternion.Y, data_hand.Quaternion.Z, data_hand.Quaternion.W);
			}
		}

		// q_bicep	  = Quaternion.Inverse( ref_bicep ) * q_bicep;
		// q_forearm = Quaternion.Inverse( ref_forearm ) * q_forearm;
		// q_hand	  = Quaternion.Inverse( ref_hand ) * q_hand;
	}

	public void t_pose() {
		ref_bicep	= bicep.rotation;
		ref_forearm = forearm.rotation;
		ref_hand	= hand.rotation;
	}

	public String parse_debug_stats() {
		String debug_line = "<mspace=0.75em>";
		debug_line += "      " + "  w  " + "  " + "  x  " + "  " + "  y  " + "  " + "  z  " + "\n";
		debug_line += "Upper " + q_bicep.w.ToString( "0.000" ) + "  " + q_bicep.y.ToString( "0.000" ) + "  " + q_bicep.z.ToString( "0.000" ) + "  " + q_bicep.x.ToString( "0.000" ) + "\n";
		debug_line += "Lower " + q_forearm.w.ToString( "0.000" ) + "  " + q_forearm.y.ToString( "0.000" ) + "  " + q_forearm.z.ToString( "0.000" ) + "  " + q_forearm.x.ToString( "0.000" ) + "\n";
		debug_line += "Hand  " + q_hand.w.ToString( "0.000" ) + "  " + q_hand.y.ToString( "0.000" ) + "  " + q_hand.z.ToString( "0.000" ) + "  " + q_hand.x.ToString( "0.000" ) + "\n";

		return debug_line;
	}

	public void end_session() {
		try {
			Debug.Log( "Closing everything..." );

			// udp_client.Close();
			// Debug.Log( "UDP client closed" );

			if( log_writer != null ) {
				log_writer.Close();
				Debug.Log( "Log writer 1 closed" );
			}

			if( sol_writer != null ) {
				sol_writer.Close();
				Debug.Log( "Log writer 2 closed" );
			}

			if( data_writer != null ) {
				data_writer.Close();
				Debug.Log( "Log writer 3 closed" );
			}

			close_tcp();

			if( network_thread.IsAlive ) {
				network_thread.Abort();
				Debug.Log( "Network thread aborted" );
			}
		} catch( Exception e ) {
			Debug.LogException( e, this );
		}
	}

	void Start() {
		// Setup the simulation
		rate		   = 1 - Time.deltaTime;
		max_cams	   = Camera.allCamerasCount;
		working_camera = Camera.allCameras[0];
		mainPlayer	   = GetComponent<Rigidbody>();
		mass_choice	   = PlayerPrefs.GetInt( "mass" );

		gender_choice = PlayerPrefs.GetString( "gender" );

		hand_choice = PlayerPrefs.GetString( "hand" );
		if( hand_choice == "Left" ) {
			bone_upper	= GameObject.Find( "mixamorig:LeftArm" );
			bone_lower	= GameObject.Find( "mixamorig:LeftForeArm" );
			bone_hand	= GameObject.Find( "mixamorig:LeftHand" );
			bone_finger = GameObject.Find( "mixamorig:LeftHandIndex1" );

			side_camera.transform.position = new Vector3( 1.25f, 2f, -0.5f );
			side_camera.transform.rotation = Quaternion.Euler( 0, -45f, 0 );
		} else {
			bone_upper	= GameObject.Find( "mixamorig:RightArm" );
			bone_lower	= GameObject.Find( "mixamorig:RightForeArm" );
			bone_hand	= GameObject.Find( "mixamorig:RightHand" );
			bone_finger = GameObject.Find( "mixamorig:RightHandIndex1" );

			side_camera.transform.position = new Vector3( -1.25f, 2f, -0.5f );
			side_camera.transform.rotation = Quaternion.Euler( 0, 45f, 0 );
		}
		name_choice = PlayerPrefs.GetString( "name" );

		mainPlayer.mass = mass_choice;
		ip_choice		= PlayerPrefs.GetString( "ip" );
		server_ip		= ip_choice;

		bicep	= bone_upper.transform;
		hand	= bone_hand.transform;
		forearm = bone_lower.transform;
		finger	= bone_finger.transform;

		log_time = DateTime.Now;

		// start new thread to receive data
		network_thread				= new Thread( new ThreadStart( get_network_data ) );
		network_thread.IsBackground = true;

		network_thread.Start();
	}

	void show_combos() {
		Debug.LogFormat( "Combination {0} is {1}, Order {2} is {3}", quat_index, quat_combos[quat_index], order_index, quat_order[order_index] );
	}

	void close_tcp() {
		if( tcp_stream != null ) {
			tcp_stream.Close();
			tcp_stream.Dispose();
			Debug.Log( "TCP stream closed" );
		}

		if( tcp_client.Connected ) {
			tcp_client.Close();
			Debug.Log( "TCP client closed" );
		}

		disconnect = false;
	}

	void get_network_data() {
		int	 failed_connections = 0;
		bool keep_retry			= true;

		while( true ) {
			// is the network connected?
			if( keep_retry && !tcp_client.Connected ) {
				try {
					tcp_client = new TcpClient( server_ip, 9022 );
					Debug.LogFormat( "Connected to client {0}", tcp_client.Client.RemoteEndPoint );

					tcp_stream = tcp_client.GetStream();
				} catch( Exception e ) {
					Debug.LogException( e, this );
					failed_connections++;

					if( failed_connections > 10 ) {
						keep_retry = false;
					}
				}
			}

			if( disconnect ) {
				close_tcp();
			}

			if( tcp_client.Connected ) {
				try {
					int bbyytteess = tcp_stream.Read( rec_data_len, 0, 4 );
					int l		   = BitConverter.ToInt32( rec_data_len, 0 );

					// Read the data packet
					Byte[] proto_rec_data = new Byte[l];
					bbyytteess			  = tcp_stream.Read( proto_rec_data, 0, proto_rec_data.Length );

					// print the received bytes
					bool data_in = get_float_array_from_proto( proto_rec_data );

					if( data_in && disconnect ) {
						Debug.Log( "Server has requested a disconnect." );
					} else if( !data_in ) {
						Debug.LogFormat( "Bad packet: {0}", bad_packet_counter );
						bad_packet_counter++;
						continue;
					}
					allocate_devices();
					log_packet();
				} catch {
					// Debug.Log( "Server has disconnected" );
					// end_session();
					// SceneManager.LoadScene( "Menu" );
				}
			}
		}
	}

	public void back_to_main_menu() {
		try {
			end_session();
			SceneManager.LoadScene( "Menu" );
		} catch( Exception e ) {
			Debug.LogException( e, this );
		}
	}

	// Update is called once per frame
	void Update() {
		if( Input.GetKey( "escape" ) ) {
			end_session();
			SceneManager.LoadScene( "Menu" );
		}

		if( Input.GetKeyDown( KeyCode.R ) ) {
			// Print the rotation between forarm and bicep
		}
		if( Input.GetKeyDown( KeyCode.H ) ) {
			// Change combination to next one
			quat_index += 1;
			if( quat_index > 15 ) {
				quat_index = 0;
			}
			show_combos();
		}
		if( Input.GetKeyDown( KeyCode.G ) ) {
			// Change combination to previous one
			quat_index -= 1;
			if( quat_index < 0 ) {
				quat_index = 15;
			}
			show_combos();
		}

		if( Input.GetKeyDown( KeyCode.T ) ) {
			t_pose();
		}

		if( Input.GetKeyDown( KeyCode.K ) ) {
			order_index += 1;
			if( order_index > 15 ) {
				quat_index += 1;
				if( quat_index > 15 ) {
					quat_index = 0;
				}
				order_index = 0;
			}
			show_combos();
		}

		if( Input.GetKeyDown( KeyCode.J ) ) {
			order_index -= 1;
			if( order_index < 0 ) {
				order_index = 15;
				quat_index -= 1;
				if( quat_index < 0 ) {
					quat_index = 15;
				}
			}
			show_combos();
		}

		// Check if audio play was started and has ended
		if( logging && !audioSource.isPlaying ) {
			logging			 = false;
			first_data_point = true;
			close_writers();
		}

		try {
			bicep.transform.rotation   = Quaternion.Lerp( bicep.transform.rotation, q_bicep, rate );
			forearm.transform.rotation = Quaternion.Lerp( forearm.transform.rotation, q_forearm, rate );
			hand.transform.rotation	   = Quaternion.Lerp( hand.transform.rotation, q_hand, rate );
		} catch( Exception err ) {
			err.ToString();
		}

		angle_elbow = 180 - Quaternion.Angle( bicep.rotation, forearm.rotation );
		angle_wrist = 180 - Quaternion.Angle( forearm.rotation, hand.rotation );

		text_stats.text	 = String.Format( "<mspace=0.75em>Bicep - Forearm\t{0}\nForearm - Hand\t{1}", angle_elbow.ToString( "0.000" ), angle_wrist.ToString( "0.000" ) );
		debug_stats.text = parse_debug_stats();
		text_stats.text += "\n</mspace>" + ( logging ? "Logging" : "Not logging" );
	}

	void FixedUpdate() {
		if( Physics.OverlapSphere( groundCheckTransform.position, 0.1f ).Length <= 1 ) {
			return;
		}
	}

	void OnApplicationQuit() {
		end_session();
	}

	void close_writers() {
		if( log_writer != null ) {
			log_writer.Close();
			Debug.Log( "Log writer 1 closed" );
		}

		if( sol_writer != null ) {
			sol_writer.Close();
			Debug.Log( "Log writer 2 closed" );
		}

		if( data_writer != null ) {
			data_writer.Close();
			Debug.Log( "Log writer 3 closed" );
		}
	}
}
