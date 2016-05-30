﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Valve.VR;

public class SteamVR_TouchpadWalking : MonoBehaviour {
    public float maxWalkSpeed = 3f;
    public float deceleration = 0.1f;
    public float headsetYOffset = 0.2f;
    public bool ignoreGrabbedCollisions = true;

    private float movementSpeed = 0f;
    private float strafeSpeed = 0f;
    private int listenerInitTries = 0;

    private Transform headset;
    private Vector2 touchAxis;

    private Rigidbody rb;
    private BoxCollider bc;

    private GameObject floorTouching;
    private Vector3 lastGoodPosition;
    private bool lastGoodPositionSet = false;
    private float highestHeadsetY = 0f;
    private float crouchMargin = 0.5f;
    private float lastPlayAreaY = 0f;

    private float retryListenersDelay = 0.25f;
    private float retryListenerMultiplier = 1.2f;
    private int listenerInitMax = 5;
    private List<int> connectedControllers;
    private List<int> foundControllerEvents;

    private void Start () {
        this.name = "PlayerObject_" + this.name;
        listenerInitTries = listenerInitMax;
        connectedControllers = new List<int>();
        foundControllerEvents = new List<int>();
        lastGoodPositionSet = false;
        headset = GetHeadset();
        CreateCollider();
        InitHeadsetListeners();
        SteamVR_Utils.Event.Listen("device_connected", OnDeviceConnected);
    }

    private Transform GetHeadset()
    {
#if (UNITY_5_4_OR_NEWER)
        return GameObject.FindObjectOfType<SteamVR_Camera>().GetComponent<Transform>();
#endif
        return GameObject.FindObjectOfType<SteamVR_GameView>().GetComponent<Transform>();
    }

    private void InitControllerListeners()
    {
        SteamVR_ControllerEvents[] controllers = GameObject.FindObjectsOfType<SteamVR_ControllerEvents>();
        if (controllers.Length != connectedControllers.Count)
        {
            if (listenerInitTries > 0)
            {
                listenerInitTries--;
            }
            else
            {
                retryListenersDelay = retryListenersDelay * retryListenerMultiplier;
                listenerInitTries = listenerInitMax;
                Debug.LogWarning("Waiting for controllers to initialise, retrying in " + retryListenersDelay);
            }
            Invoke("InitControllerListeners", retryListenersDelay);
        }
        else
        {
            foreach (SteamVR_ControllerEvents controller in controllers)
            {
                int controllerEventControllerIndex = (int)controller.GetControllerIndex();
                if (!foundControllerEvents.Contains(controllerEventControllerIndex))
                {
                    controller.TouchpadAxisChanged += new ControllerClickedEventHandler(DoTouchpadAxisChanged);
                    controller.TouchpadUntouched += new ControllerClickedEventHandler(DoTouchpadUntouched);

                    if (ignoreGrabbedCollisions && controller.GetComponent<SteamVR_InteractGrab>())
                    {
                        SteamVR_InteractGrab grabbingController = controller.GetComponent<SteamVR_InteractGrab>();
                        grabbingController.ControllerGrabInteractableObject += new ObjectInteractEventHandler(OnGrabObject);
                        grabbingController.ControllerUngrabInteractableObject += new ObjectInteractEventHandler(OnUngrabObject);
                    }
                    foundControllerEvents.Add(controllerEventControllerIndex);
                }
            }
        }
    }

    private void InitHeadsetListeners()
    {
        if (headset.GetComponent<SteamVR_HeadsetCollisionFade>())
        {
            headset.GetComponent<SteamVR_HeadsetCollisionFade>().HeadsetCollisionDetect += new HeadsetCollisionEventHandler(OnHeadsetCollision);
        }
    }

    private void OnGrabObject(object sender, ObjectInteractEventArgs e)
    {
        Physics.IgnoreCollision(this.GetComponent<Collider>(), e.target.GetComponent<Collider>(), true);
    }

    private void OnUngrabObject(object sender, ObjectInteractEventArgs e)
    {
        Physics.IgnoreCollision(this.GetComponent<Collider>(), e.target.GetComponent<Collider>(), false);
    }

    private void OnHeadsetCollision(object sender, HeadsetCollisionEventArgs e)
    {
        if (lastGoodPositionSet) {
            SteamVR_Fade.Start(Color.black, 0f);
            this.transform.position = lastGoodPosition;
        }
    }

    private void CreateCollider()
    {
        rb = this.gameObject.AddComponent<Rigidbody>();
        rb.mass = 100;
        rb.freezeRotation = true;

        bc = this.gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 1f, 0f);
        bc.size = new Vector3(0.25f, 1f, 0.25f);

        this.gameObject.layer = 2;
    }

    private void DoTouchpadAxisChanged(object sender, ControllerClickedEventArgs e)
    {
        touchAxis = e.touchpadAxis;
    }

    private void DoTouchpadUntouched(object sender, ControllerClickedEventArgs e)
    {
        touchAxis = Vector2.zero;
    }

    private void CalculateSpeed(ref float speed, float inputValue)
    {
        if (inputValue != 0f)
        {
            speed = (maxWalkSpeed * inputValue);
        }
        else
        {
            Decelerate(ref speed);
        }
    }

    private void Decelerate(ref float speed)
    {
        if (speed > 0)
        {
            speed -= Mathf.Lerp(deceleration, maxWalkSpeed, 0f);
        }
        else if (speed < 0)
        {
            speed += Mathf.Lerp(deceleration, -maxWalkSpeed, 0f);
        }
        else
        {
            speed = 0;
        }

        float deadzone = 0.1f;
        if (speed < deadzone && speed > -deadzone)
        {
            speed = 0;
        }
    }

    private void Move()
    {
        Vector3 movement = headset.transform.forward * movementSpeed * Time.deltaTime;
        Vector3 strafe = headset.transform.right * strafeSpeed * Time.deltaTime;
        float fixY = this.transform.position.y;
        this.transform.position += (movement + strafe);
        this.transform.position = new Vector3(this.transform.position.x, fixY, this.transform.position.z);
    }

    private void UpdateCollider()
    {
        float playAreaHeightAdjustment = 0.009f;
        float newBCYSize = (headset.transform.position.y - headsetYOffset) - this.transform.position.y;
        float newBCYCenter = (newBCYSize != 0 ? (newBCYSize / 2) + playAreaHeightAdjustment: 0);

        bc.size = new Vector3(bc.size.x, newBCYSize, bc.size.z);
        bc.center = new Vector3(headset.localPosition.x, newBCYCenter, headset.localPosition.z);
    }

    private void SetHeadsetY()
    {
        //if the play area height has changed then always recalc headset height
        float floorVariant = 0.005f;
        if (this.transform.position.y > lastPlayAreaY + floorVariant || this.transform.position.y < lastPlayAreaY - floorVariant)
        {
            highestHeadsetY = 0f;
        }

        if (headset.transform.position.y > highestHeadsetY)
        {
            highestHeadsetY = headset.transform.position.y;
        }

        if (headset.transform.position.y > highestHeadsetY - crouchMargin)
        {
            lastGoodPositionSet = true;
            lastGoodPosition = this.transform.position;
        }

        lastPlayAreaY = this.transform.position.y;
    }

    private void Update()
    {
        SetHeadsetY();
        UpdateCollider();
    }

    private void FixedUpdate()
    {
        CalculateSpeed(ref movementSpeed, touchAxis.y);
        CalculateSpeed(ref strafeSpeed, touchAxis.x);
        Move();
    }

    private void OnDeviceConnected(params object[] args)
    {
        if (IsController((uint)(int)args[0]))
        {
            if ((bool)args[1])
            {
                connectedControllers.Add((int)args[0]);
            }
            else
            {
                connectedControllers.Remove((int)args[0]);
            }
            Invoke("InitControllerListeners", 0.5f);
        }
    }

    private bool IsController(uint index)
    {
        var system = OpenVR.System;
        return (system != null && system.GetTrackedDeviceClass(index) == ETrackedDeviceClass.Controller);
    }
}