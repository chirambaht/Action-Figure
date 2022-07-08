using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cam_controller : MonoBehaviour
{
    Vector3 touchStart;
    Vector3 mouseStart;
    const float zoomOutMin = 0.1f;
    const float zoomOutMax = 1.5f;
    const float zoomFactor = 0.1f;

    Camera working_camera;
    Camera[] all_cameras = new Camera[3];
    int camera_index = 0;
    int camera_count = 0;

    bool ui_show = true;
    GameObject text_ui;
    GameObject[] button_ui = new GameObject[2];

    bool three_cam_view = true;
    // 0 - All 3 cameras    x   y   w   h
    // 1 - main camera      0.5 0   0.5 1
    // 2 - top camera       0   0.5 0.5 0.5
    // 3 - side camera      0   0   0.5 0.5

    void Start()
    {
        camera_count = Camera.allCamerasCount;
        camera_index = 0;
        all_cameras = Camera.allCameras;
        working_camera = Camera.allCameras[camera_index];

        text_ui = GameObject.Find("Text Overlay");
        button_ui[0] = GameObject.Find("swap_cam");
        button_ui[1] = GameObject.Find("big_cam");
    }

    public void swap_camera()
    {
        if (camera_index < camera_count - 1)
        {
            camera_index++;
        }
        else
        {
            camera_index = 0;
        }
        working_camera = all_cameras[camera_index];
        tri_camera(false);
        Debug.LogFormat("Camera changed to {0}/{1}:{2}", camera_index + 1, camera_count, working_camera.name);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            swap_camera();
        }

        if (Input.GetMouseButtonDown(0))
        {
            touchStart = working_camera.ScreenToWorldPoint(Input.mousePosition);
        }

        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float currentMagnitude = (touchZero.position - touchOne.position).magnitude;

            float difference = currentMagnitude - prevMagnitude;

            zoom(difference * 0.01f);
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 direction = touchStart - working_camera.ScreenToWorldPoint(Input.mousePosition);
            working_camera.transform.position += direction;
        }
        else if (Input.GetMouseButton(1))
        {
            float mag = (Input.mousePosition - mouseStart).magnitude;
            if (Input.mousePosition.y > mouseStart.y)
            {
                mag *= -1;
            }
            working_camera.transform.Rotate(new Vector3(mag, 0, 0));
            mouseStart = Input.mousePosition;
        }
        zoom(Input.GetAxis("Mouse ScrollWheel"));
    }

    void zoom(float increment)
    {
        increment = increment * zoomFactor;
        working_camera.orthographicSize =
            Mathf.Clamp(working_camera.orthographicSize - increment, zoomOutMin, zoomOutMax);
    }

    public void Ive_been_touched()
    {
        Debug.Log("I've been touched");
    }

    public void change_ui()
    {
        if (ui_show)
        {
            Debug.Log("Hiding the UI");
        }
        else
        {
            Debug.Log("Showing the UI");
        }
        ui_show = !ui_show;
        text_ui.SetActive(ui_show);
        // for (int i = 0; i < button_ui.Length; i++){
        //     button_ui[i].SetActive(ui_show);
        // }
    }

    public void tri_camera(bool to_change = true)
    {

        // 3 - All 3 cameras    x   y   w   h
        // 0 - main camera      0.5 0   0.5 1
        // 1 - top camera       0   0.5 0.5 0.5
        // 2 - side camera      0   0   0.5 0.5

        if (to_change)
        {
            three_cam_view = !three_cam_view;
        }
        if (three_cam_view)
        {
            Debug.Log("Showing 3 cameras");
            all_cameras[0].rect = new Rect(0.5f, 0f, 0.5f, 1f);
            all_cameras[1].rect = new Rect(0f, 0.5f, 0.5f, 0.5f);
            all_cameras[2].rect = new Rect(0f, 0f, 0.5f, 0.5f);

            for (int i = 0; i < camera_count; i++)
            {
                all_cameras[i].enabled = true;
            }
        }
        else
        {
            Debug.Log("Showing 1 camera");
            all_cameras[0].rect = new Rect(0f, 0f, 1f, 1f);
            all_cameras[1].rect = new Rect(0f, 0f, 1f, 1f);
            all_cameras[2].rect = new Rect(0f, 0f, 1f, 1f);

            for (int i = 0; i < camera_count; i++)
            {
                if (camera_index == i)
                {
                    all_cameras[i].enabled = true;
                }
                else
                {
                    all_cameras[i].enabled = false;
                }
            }
        }
    }

    public void rotate_main_cam_by_180_degrees()
    {
        if (working_camera == Camera.main)
        {
            Camera.main.transform.Rotate(new Vector3(0, 180, 0));
            Camera.main.transform.position =
                new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y,
                            Camera.main.transform.position.z * -1);
        }
    }
}