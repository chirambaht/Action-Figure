using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
// using UnityEngine.
using UnityEngine.SceneManagement;
using TMPro;
using System.Net;

public class menu : MonoBehaviour {
	public GameObject	selected_sprite;
	public TMP_Dropdown gender_dropdown;
	public TMP_Dropdown hand_dropdown;

	public TMP_InputField participant_name;
	public TMP_InputField participant_mass;
	public TMP_InputField ip_input;

	List<string> genders = new List<string>( new string[] { "Male", "Female" } );
	List<string> hands	 = new List<string>( new string[] { "Left", "Right" } );

	public void exit() {
		Debug.Log( "Game closed" );
		Application.Quit();
	}

	public void start_data_collection() {
		Debug.LogFormat( "Name: {0}\nMass: {1}\nGender: {2}\nHand: {3}", participant_name.text, participant_mass.text, genders[gender_dropdown.value], hands[hand_dropdown.value] );
		PlayerPrefs.SetString( "hand", hands[hand_dropdown.value] );
		PlayerPrefs.SetString( "gender", genders[gender_dropdown.value] );
		PlayerPrefs.SetString( "name", participant_name.text );
		PlayerPrefs.SetString( "ip", ip_input.text );
		PlayerPrefs.SetInt( "mass", int.Parse( participant_mass.text ) );

		Invoke( "scene_swap", 10f );
		// if( genders[gender_dropdown.value] == "Male" ) {
		// 	SceneManager.LoadScene( "Action Scene Male" );
		// } else {
		// 	SceneManager.LoadScene( "Action Scene Female" );
		// }
	}

	void scene_swap() {
		if( PlayerPrefs.GetString( "gender" ) == "Male" ) {
			SceneManager.LoadScene( "Action Scene Male" );
		} else {
			SceneManager.LoadScene( "Action Scene Female" );
		}
	}
	// public void change_sprite_active( GameObject sprite, bool active_level ) {
	// 	sprite.GetComponent<Renderer>().enabled = active_level;
	// 	sprite.SetActive( true );
	// }

	public void select_sprite() {
		// read dropdown menu
		Vector3 pozi = selected_sprite.transform.position;
		if( selected_sprite == GameObject.Find( "xbot" ) ) {
			selected_sprite.transform.position = new Vector3( -200.0f, 5000.0f, -50.0f );
			selected_sprite					   = GameObject.Find( "ybot" );
			selected_sprite.transform.position = pozi;
		} else {
			selected_sprite.transform.position = new Vector3( -200.0f, 5000.0f, 50.0f );
			selected_sprite					   = GameObject.Find( "xbot" );
			selected_sprite.transform.position = pozi;
		}

		// set sprite
		// change game object position
	}

	// Start is called before the first frame update
	void Start() {
		gender_dropdown.AddOptions( genders );
		hand_dropdown.AddOptions( hands );

		ip_input.text = PlayerPrefs.GetString( "ip" );

		// foreach (var item in (Dns.GetHostEntry(Dns.GetHostName()).AddressList))
		// {
		//     ips.Add(item.ToString());
		// }
		// ip_dropdown.AddOptions(ips);
	}

	// Update is called once per frame
	void Update() {
	}
}
