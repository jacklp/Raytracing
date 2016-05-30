using UnityEngine;
using System.Collections;

public class RayTrace : MonoBehaviour {

	public Texture2D screen;
	public Light[] lightsArr;
	public int Maxstack;

	private RaycastHit rayTraceHit;
	private Color rayTraceColour;
	private Material rayTraceMat;
	private LayerMask collisionMask = 1 << 31;
	private Material transparencyMat;
	private float fl;
	private float tmpfl;
	private Ray ray;
	private Shader reflectiveShader;

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

		reflectiveShader = Shader.Find("Specular");
		lightsArr = FindObjectsOfType (typeof(Light)) as Light[];

		for (int x = 0; x < screen.width; x ++) {
			for (int y = 0; y < screen.height; y ++) {
				screen.SetPixel (x, y, TracePixel (new Vector2 (x, y)));
			}
		}
		screen.Apply ();
	}

	Color TracePixel(Vector2 pos) {
		ray = GetComponent<Camera>().ScreenPointToRay(new Vector3(pos.x, pos.y, 0));
		return TraceRay(ray.origin, ray.direction, 0);
	}

	Color TraceRay(Vector3 origin, Vector3 direction, int stack){

		// Raycast hit
		if(stack < Maxstack && Physics.Raycast (origin, direction, out rayTraceHit, GetComponent<Camera> ().farClipPlane, collisionMask)){

			//defensive programming
			if (rayTraceHit.collider != null && rayTraceHit.collider.transform.parent != null) {

				//Get material
				if (rayTraceHit.collider.transform.parent.GetComponent<MeshFilter>().mesh.subMeshCount > 1) {
					rayTraceMat = rayTraceHit.collider.transform.parent.GetComponent<Renderer>().materials[GetMatFromTrisInMesh(rayTraceHit.collider.transform.parent.GetComponent<MeshFilter>().mesh, rayTraceHit.triangleIndex)];
				}
				else {
					rayTraceMat = rayTraceHit.collider.transform.parent.GetComponent<Renderer> ().material;
				}
					
				//Get Pixel colour
				if (rayTraceMat.mainTexture) {
					Texture2D mainTex = rayTraceMat.mainTexture as Texture2D;
					rayTraceColour = mainTex.GetPixelBilinear (rayTraceHit.textureCoord.x, rayTraceHit.textureCoord.y);
				} else {
					rayTraceColour = rayTraceMat.color;
				}

				Mesh tempMesh = rayTraceHit.collider.transform.parent.GetComponent<MeshFilter>().mesh as Mesh;
				rayTraceColour *= TraceLight(rayTraceHit.point+rayTraceHit.normal*0.0001f, InterpolateNormal(rayTraceHit.point, rayTraceHit.normal, tempMesh, rayTraceHit.triangleIndex, rayTraceHit.transform));

				rayTraceColour.a = 1;

			} else {
				//debugging
				return Color.red;
			}
		} else {
			if (RenderSettings.skybox) {
				rayTraceColour = SkyboxTrace (direction, RenderSettings.skybox);
				rayTraceColour += Color.white * (1 - rayTraceColour.a) / 10;
				rayTraceColour.a = 1;

				return rayTraceColour;
			} else {
				rayTraceColour = Color.black;
			}
		}
		return rayTraceColour;
	}

	Color SkyboxTrace(Vector3 direction, Material skybox){
		if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) {
			if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z)) {
				if (direction.x < 0) {
					return (skybox.GetTexture("_LeftTex") as Texture2D).GetPixelBilinear((-direction.z/-direction.x+1)/2, (direction.y/-direction.x+1)/2);
				}
				else{
					return (skybox.GetTexture("_RightTex") as Texture2D).GetPixelBilinear((direction.z/direction.x+1)/2, (direction.y/direction.x+1)/2);
				}
			}
			else{
				if (direction.z < 0) {
					return (skybox.GetTexture("_BackTex") as Texture2D).GetPixelBilinear((direction.x/-direction.z+1)/2, (direction.y/-direction.z+1)/2);
				}
				else{
					return (skybox.GetTexture("_FrontTex") as Texture2D).GetPixelBilinear((-direction.x/direction.z+1)/2, (direction.y/direction.z+1)/2);
				}
			}
		}
		else if (Mathf.Abs(direction.y) > Mathf.Abs(direction.z)){
			if (direction.y < 0) {
				return (skybox.GetTexture("_DownTex") as Texture2D).GetPixelBilinear((-direction.x/-direction.y+1)/2, (direction.z/-direction.y+1)/2);
			}
			else{
				return (skybox.GetTexture("_UpTex") as Texture2D).GetPixelBilinear((-direction.x/direction.y+1)/2, (-direction.z/direction.y+1)/2);
			}
		}
		else{
			if (direction.z < 0) {
				return (skybox.GetTexture("_BackTex") as Texture2D).GetPixelBilinear((direction.x/-direction.z+1)/2, (direction.y/-direction.z+1)/2);
			}
			else{
				return (skybox.GetTexture("_FrontTex") as Texture2D).GetPixelBilinear((-direction.x/direction.z+1)/2, (direction.y/direction.z+1)/2);
			}
		}
	}

	//Blend colour between the vector points
	Vector3 InterpolateNormal(Vector3 point, Vector3 normal, Mesh mesh, int trisIndex, Transform trans){
		int index = mesh.triangles[trisIndex*3];
		int index2 = mesh.triangles[trisIndex*3+1];
		int index3 = mesh.triangles[trisIndex*3+2];

		int tmpIndex;

		float d1 = Vector3.Distance(mesh.vertices[index], point);
		float d2 = Vector3.Distance(mesh.vertices[index2], point);
		float d3= Vector3.Distance(mesh.vertices[index3], point);

		if (d2 > d1 && d2 > d3) {
			tmpIndex = index;
			index = index2;
			index2 = tmpIndex;
		}
		else if (d3 > d1 && d3 > d2) {
			tmpIndex = index;
			index = index3;
			index3 = tmpIndex;
			tmpIndex = index2;
			index2 = index3;
			index3 = tmpIndex;
		}

		Plane plane = new Plane(trans.TransformPoint(mesh.vertices[index2]), trans.TransformPoint(mesh.vertices[index3])+normal, trans.TransformPoint(mesh.vertices[index3])-normal);
		Ray ray = new Ray(trans.TransformPoint(mesh.vertices[index]), (point - trans.TransformPoint(mesh.vertices[index])).normalized);

		if (!plane.Raycast(ray, out tmpfl)) {
			return normal;
		}

		Vector3 point2 = ray.origin+ray.direction*tmpfl;
		Vector3 normal2 = Vector3.Lerp(trans.TransformDirection(mesh.normals[index2]), trans.TransformDirection(mesh.normals[index3]), Vector3.Distance(trans.TransformPoint(mesh.vertices[index2]), point2)/Vector3.Distance(trans.TransformPoint(mesh.vertices[index2]), trans.TransformPoint(mesh.vertices[index3])));
		Vector3 normal3 = Vector3.Lerp(normal2, trans.TransformDirection(mesh.normals[index]), Vector3.Distance(point2, point)/Vector3.Distance(point2, trans.TransformPoint(mesh.vertices[index])));
		return normal3;
	}

	Color TraceLight(Vector3 pos, Vector3 normal){
		Color traceLightColour = RenderSettings.ambientLight;

		foreach (Light light in lightsArr) {
			traceLightColour += LightTrace (light, pos, normal);
		}

		return traceLightColour;
	}

	Color LightTrace(Light light, Vector3 pos, Vector3 normal){
		if (light.type == LightType.Directional) {
			Vector3 direction = light.transform.TransformDirection (Vector3.back);
			return TransparencyTrace (new Color (light.intensity, light.intensity, light.intensity) *
			(1 - Quaternion.Angle (Quaternion.identity, Quaternion.FromToRotation (normal, direction)) / 90), pos, direction, Mathf.Infinity);
		}

		if (light.type == LightType.Point) {
			if (Vector3.Distance(pos, light.transform.position) <= light.range) {

				Vector3 direction = (light.transform.position - pos).normalized;

				fl = (light.range-Vector3.Distance(pos, light.transform.position))/light.range*light.intensity;

				return TransparencyTrace(new Color(fl, fl, fl)*(1-Quaternion.Angle(Quaternion.identity, Quaternion.FromToRotation(normal, direction))/90), pos, direction, Vector3.Distance(light.transform.position, pos));
			}
		}

		if (light.type == LightType.Spot) {
			if (Vector3.Distance(pos, light.transform.position) <= light.range) {
				Vector3 direction = (light.transform.position - pos).normalized;
				if (Vector3.Angle(direction, -light.transform.forward) < light.spotAngle) {
					fl = (light.range-Vector3.Distance(pos, light.transform.position))/light.range*light.intensity;
					fl *= 1 - Vector3.Angle(direction, -light.transform.forward)/light.spotAngle;
					return TransparencyTrace(new Color(fl, fl, fl)*(1-Quaternion.Angle(Quaternion.identity, Quaternion.FromToRotation(normal, direction))/90), pos, direction, Vector3.Distance(light.transform.position, pos));
				}
			}
		}
		return Color.black;
	}
		
	Color TransparencyTrace(Color col, Vector3 pos, Vector3 dir, float dist){

		Color transparencyColour = col;
		RaycastHit[] hits = Physics.RaycastAll(pos, dir, dist, collisionMask);
		foreach (RaycastHit hit in hits) {

			//Get Mat
			if (hit.collider.transform.parent.GetComponent<MeshFilter> ().mesh.subMeshCount > 1) {
				transparencyMat = hit.collider.transform.parent.GetComponent<Renderer> ().materials [GetMatFromTrisInMesh (hit.collider.transform.parent.GetComponent<MeshFilter> ().mesh, hit.triangleIndex)];
			} else {
				transparencyMat = hit.collider.transform.parent.GetComponent<Renderer>().material;
			}

			//Get Texture
			if (transparencyMat.mainTexture) {
				Texture2D tex = (transparencyMat.mainTexture as Texture2D);
				transparencyColour *= 1-tex.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y).a;
			}
			else {
				transparencyColour *= 1-transparencyMat.color.a;
			}
		}
		return transparencyColour;
	}

	int GetMatFromTrisInMesh(Mesh mesh, int trisIndex){
		int[] tri = new int[]{mesh.triangles[trisIndex*3], mesh.triangles[trisIndex*3+1], mesh.triangles[trisIndex*3+2]};

		for (int index = 0; index < mesh.subMeshCount; index++) {
			int[] tris = mesh.GetTriangles (index);
				for (int index2 = 0; index2 < tris.Length; index2 += 3) {
					if (tris[index2] == tri[0] && tris[index2+1] == tri[1] && tris[index2+2] == tri[2]) {
						return index;
					}
				}
		}
		//this shouldn't ever happen but satisfies compiler
		return 0;
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
}