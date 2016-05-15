using UnityEngine;
using System.Collections;

public class RayTrace : MonoBehaviour {

	public Texture2D screen;
	public Light[] lightsArr;
	public int Maxstack;

	private LayerMask collisionMask = 1 << 31;
	private Color result;
	private RaycastHit hit;
	private Material mat;

	void Awake(){
		screen = new Texture2D (Screen.width, Screen.height);
		Maxstack = 2;
	}

	void OnGUI(){
		GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), screen);
	}

	void Start(){

		foreach (MeshFilter mf in FindObjectsOfType(typeof(MeshFilter)) as MeshFilter[]) {
			GenerateColliders(mf);
		}

		lightsArr = FindObjectsOfType (typeof(Light)) as Light[];

		for (int x = 0; x < screen.width; x ++) {
			for (int y = 0; y < screen.height; y ++) {
				screen.SetPixel (x, y, RayTracePixel (new Vector2 (x, y), 0));
			}
		}
		screen.Apply ();
	}

	void GenerateColliders(MeshFilter mf){
		GameObject clone = new GameObject("MeshClone");
		clone.transform.parent = mf.transform;

		MeshFilter meshFilterClone = clone.AddComponent (typeof(MeshFilter)) as MeshFilter;
		meshFilterClone.mesh = mf.mesh;

		MeshCollider meshColliderClone = clone.AddComponent (typeof(MeshCollider)) as MeshCollider;
		meshColliderClone.sharedMesh = mf.mesh;

		clone.transform.localPosition = Vector3.zero;
		clone.transform.localScale = Vector3.one;
		clone.transform.rotation = mf.transform.rotation;

		clone.layer = 31;

	}

	Color RayTracePixel(Vector2 pos, int stack){
	
		Ray ray = GetComponent<Camera> ().ScreenPointToRay (new Vector3 (pos.x, pos.y, 0));

		if(stack < Maxstack && Physics.Raycast (ray.origin, ray.direction, out hit, GetComponent<Camera> ().farClipPlane, collisionMask)){
			if(hit.collider && hit.collider.transform.parent){
				mat = hit.collider.transform.parent.GetComponent<Renderer>().material;
			}

			if (!mat.mainTexture.Equals(null)) {
				Texture2D mainTex = mat.mainTexture as Texture2D;
				result = mainTex.GetPixelBilinear (hit.textureCoord.x, hit.textureCoord.y);
			} else {
				result = mat.color;
			}
		} else {
			result = Color.black;
		}
		return result;
	}
}