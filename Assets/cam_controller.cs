using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cam_controller : MonoBehaviour {
    Vector3 touchStart;
    Vector3 mouseStart;
    const float zoomOutMin = 0.3f;
    const float zoomOutMax = 2;

    Camera working_camera;
    int camera_index =0;
    int camera_count =0;
	
    void Start(){
        camera_count = Camera.allCamerasCount;
        camera_index = 0;
        working_camera = Camera.allCameras[camera_index];
    }

    public void swap_camera(){
         if (camera_index < camera_count - 1){
            camera_index++;
        }
        else{
            camera_index = 0;
        }
        working_camera = Camera.allCameras[camera_index];
        Debug.LogFormat("Camera changed to {0}/{1}:{2}", camera_index+1, camera_count, working_camera.name);
    }

	// Update is called once per frame
	void Update () {
         if (Input.GetKeyDown(KeyCode.C))
        {
           swap_camera();
            
        }

        if(Input.GetMouseButtonDown(0)){
            touchStart = working_camera.ScreenToWorldPoint(Input.mousePosition);
        }

        if(Input.touchCount == 2){
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float currentMagnitude = (touchZero.position - touchOne.position).magnitude;

            float difference = currentMagnitude - prevMagnitude;

            zoom(difference * 0.01f);
        }else if(Input.GetMouseButton(0)){
            Vector3 direction = touchStart - working_camera.ScreenToWorldPoint(Input.mousePosition);
            working_camera.transform.position += direction;
        }
        else if(Input.GetMouseButton(1)){
            float mag = (Input.mousePosition - mouseStart).magnitude;
            if (Input.mousePosition.y > mouseStart.y ){
                mag *= -1;
            }
            working_camera.transform.Rotate(  new Vector3(mag,0,0)) ;
            mouseStart = Input.mousePosition;
        }
        zoom(Input.GetAxis("Mouse ScrollWheel"));
	}

    void zoom(float increment){
        working_camera.orthographicSize = Mathf.Clamp(working_camera.orthographicSize - increment, zoomOutMin, zoomOutMax);
    }

    public void Ive_been_touched(){
        Debug.Log("I've been touched");
    }
}