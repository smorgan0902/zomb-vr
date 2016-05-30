using UnityEngine;
using System.Collections;

public class BeACreep : MonoBehaviour {

    public GameObject player;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        transform.LookAt(player.transform);
        transform.position += (player.transform.position * 0.01f - transform.position) * 0.001f;
	}
}
