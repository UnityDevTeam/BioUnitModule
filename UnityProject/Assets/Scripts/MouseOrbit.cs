using UnityEngine;

//[ExecuteInEditMode]
public class MouseOrbit : MonoBehaviour
{
	public Vector3 target;
	public Vector3 anchor;	

	public float xSpeed = 250.0f;
	public float ySpeed = 120.0f;
	
	public float yMinLimit = -20f;
	public float yMaxLimit = 90f;

	public float DEFAULT_DISTANCE = 5.0f;

	public Vector3 cullReactionPlaneNormal;

	public float zoom = 1;

//	private Quaternion targetNornal = Vector3.up;

	void Start () 
	{
		transform.rotation = Quaternion.LookRotation(target - anchor);
		anchor = target - transform.forward * DEFAULT_DISTANCE;
		transform.position = target - transform.forward * Vector3.Distance(target, anchor) * zoom;

//		transform.rotation = Quaternion.LookRotation(target - transform.position);
//		anchor = target - transform.forward * DEFAULT_DISTANCE;
	}

	void Update () 
	{
		if(Input.GetKeyDown(KeyCode.F))
		{
			zoom = 1;
			anchor = target - transform.forward * DEFAULT_DISTANCE;
		}

		if(Input.GetKeyDown(KeyCode.L))
		{
			cullReactionPlaneNormal = transform.forward;
		}

		if (Input.GetMouseButton(1))
		{
			float x = Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
			float y = -Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime; 

			float distance = Vector3.Distance(target, anchor);

			var rotation = transform.rotation * Quaternion.Euler(y, x, 0.0f);
			anchor = rotation * new Vector3(0.0f, 0.0f, -distance) + target;
		}

		float scale = 20;

		if (Input.GetMouseButton(2))
		{
			target -= transform.up * Input.GetAxis("Mouse Y") * Time.deltaTime * scale;   
			anchor -= transform.up * Input.GetAxis("Mouse Y") * Time.deltaTime * scale;    

			target -= transform.right * Input.GetAxis("Mouse X") * Time.deltaTime * scale;    
			anchor -= transform.right * Input.GetAxis("Mouse X") * Time.deltaTime * scale;  
		}

		if (Input.GetKey(KeyCode.W))
		{
			target += transform.forward * Time.deltaTime * scale;  
			anchor += transform.forward * Time.deltaTime * scale;   
		}

		if (Input.GetKey(KeyCode.A))
		{
			target -= transform.right * Time.deltaTime * scale;   
			anchor -= transform.right * Time.deltaTime * scale;    			
		}

		if (Input.GetKey(KeyCode.D))
		{
			target += transform.right * Time.deltaTime * scale;    			
			anchor += transform.right * Time.deltaTime * scale;    			
		}

		if (Input.GetKey(KeyCode.S))
		{
			target -= transform.forward * Time.deltaTime * scale; 	 
			anchor -= transform.forward * Time.deltaTime * scale; 		
		}

		if (Input.GetAxis("Mouse ScrollWheel") != 0.0f) // forward
		{
			anchor += transform.forward * Time.deltaTime * scale * 10 * Input.GetAxis("Mouse ScrollWheel");   
		}

		transform.rotation = Quaternion.LookRotation(target - anchor);
		transform.position = target - transform.forward * Vector3.Distance(target, anchor) * zoom;
	}
	
	private float ClampAngle (float angle, float min, float max)
	{
		if (angle < -360.0f)
			angle += 360.0f;

		if (angle > 360.0f)
			angle -= 360.0f;

		return Mathf.Clamp (angle, min, max);
	}
}
