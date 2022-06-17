using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Net;

public class menu : MonoBehaviour {
	public GameObject	  selected_sprite;
	public TMP_Dropdown	  gender_dropdown;
	public TMP_Dropdown	  hand_dropdown;
	public TMP_Dropdown	  ip_dropdown;
	public TMP_InputField participant_name;
	public TMP_InputField participant_mass;
	List<string>		  genders				 = new List<string>( new string[] { "Male", "Female" } );
	List<string>		  hands					 = new List<string>( new string[] { "Left", "Right" } );
	int					  how_far_have_i_rotated = 0;

	public void exit() {
		Debug.Log( "Game closed" );
		Application.Quit();
	}

	public void start_data_collection() {
		Debug.LogFormat( "Name: {0}\nMass: {1}\nGender: {2}\nHand: {3}", participant_name.text, participant_mass.text, genders[gender_dropdown.value], hands[hand_dropdown.value] );
		PlayerPrefs.SetString( "hand", hands[hand_dropdown.value] );
		PlayerPrefs.SetString( "gender", genders[gender_dropdown.value] );
		PlayerPrefs.SetString( "name", participant_name.text );
		// PlayerPrefs.SetString( "ip", participant_name.text ); GET THIS NEXT
		PlayerPrefs.SetInt( "mass", int.Parse( participant_mass.text ) );
		SceneManager.LoadScene( "Action Scene" );
	}

	public void select_sprite() {
		// read dropdown menu
		if( selected_sprite == GameObject.Find( "xbot" ) ) {
			selected_sprite.transform.Rotate( 0, -2 * how_far_have_i_rotated, 0 );
			selected_sprite = GameObject.Find( "ybot" );
		} else {
			selected_sprite.transform.Rotate( 0, -2 * how_far_have_i_rotated, 0 );
			selected_sprite = GameObject.Find( "xbot" );
		}
		how_far_have_i_rotated = 0;
		// set sprite
	}

	// Start is called before the first frame update
	void Start() {
		gender_dropdown.AddOptions( genders );
		hand_dropdown.AddOptions( hands );
		List<string> ips = new List<string>();

		foreach( var item in( Dns.GetHostEntry( Dns.GetHostName() ).AddressList ) ) {
			ips.Add( item.ToString() );
		}
		ip_dropdown.AddOptions( ips );
		// Get machine IP address

		Debug.Log( Dns.GetHostEntry( Dns.GetHostName() ).AddressList );
	}

	// Update is called once per frame
	void Update() {
		selected_sprite.transform.Rotate( 0, selected_sprite.transform.rotation.y + 1, 0 );
		how_far_have_i_rotated++;
	}
}
