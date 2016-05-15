import System.IO;

var RealTime:boolean = true;
var AutoGenerateColliders:boolean = true;
var SmoothEdges:boolean = true;
var SingleMaterialOnly:boolean = false;
var UseLighting:boolean = true;
var resolution:float = 1;
var MaxStack:int = 2;

@System.NonSerialized
var screen:Texture2D;
private var reflectiveShader:Shader;

private var x:int;
private var y:int;

private var light:Light;
private var tris:int[];
private var tri:int[];

private var index:int;
private var index2:int;
private var index3:int;

private var ray:Ray;
private var direction:Vector3;
private var normal:Vector3;

private var tmpFloat:float;
private var tmpFloat2:float;

private var tmpTex:Texture2D;
private var tmpMat:Material;

private var tmpMeshFilter:MeshFilter;
private var tmpGameObject:GameObject;


private var lights:Light[];

private var collisionMask:LayerMask = 1 << 31;


function Start() {

	if (screen) {
		Destroy(screen);
	}

	screen = new Texture2D(Screen.width*resolution, Screen.height*resolution);

	reflectiveShader = Shader.Find("Specular");
	
	if (AutoGenerateColliders) {
		for (tmpMeshFilter in FindSceneObjectsOfType(typeof MeshFilter) as MeshFilter[]) {
			GenerateColliders(tmpMeshFilter);
		}
	}
	
	if (!RealTime) {
		RayTrace();
	}
}

function Update() {
	if (RealTime) {
		RayTrace();
	}
}

function OnGUI() {
	GUI.DrawTexture(Rect(0, 0, Screen.width, Screen.height), screen);
	GUILayout.Label("fps: " + Mathf.Round(1/Time.smoothDeltaTime));
}

function RayTrace():void {
	lights = FindSceneObjectsOfType(typeof Light) as Light[];

	for (x = 0; x < screen.width; x += 1) {
		for (y = 0; y < screen.height; y += 1) {
			screen.SetPixel(x, y, TracePixel(Vector2(x, y)));
		}
	}
	screen.Apply();
}

function TracePixel(pos:Vector2):Color {
	ray = GetComponent.<Camera>().ScreenPointToRay(Vector3(pos.x/resolution, pos.y/resolution, 0));
	return TraceRay(ray.origin, ray.direction, 0);
}

function TraceRay(origin:Vector3, direction:Vector3, stack:int):Color {
	var tmpColor:Color;
	var hit:RaycastHit;

	if (stack < MaxStack && Physics.Raycast(origin, direction, hit, GetComponent.<Camera>().farClipPlane, collisionMask)) {
		
		if (hit.collider && hit.collider.transform.parent) {
			if (hit.collider.transform.parent.GetComponent(MeshFilter).mesh.subMeshCount > 1 && !SingleMaterialOnly) {
				tmpMat = hit.collider.transform.parent.GetComponent.<Renderer>().materials[GetMatFromTrisInMesh(hit.collider.transform.parent.GetComponent(MeshFilter).mesh, hit.triangleIndex)];
			}
			else {
				tmpMat = hit.collider.transform.parent.GetComponent.<Renderer>().material;
			}

			if (tmpMat.mainTexture) {
				tmpColor = (tmpMat.mainTexture as Texture2D).GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
			}
			else {
				tmpColor = tmpMat.color;
			}

			if (tmpColor.a < 1) {
				tmpColor *= tmpColor.a;
				tmpColor += (1-tmpColor.a)*TraceRay(hit.point-hit.normal*0.01, direction, stack+1);
			}

			if (tmpMat.shader == reflectiveShader) {
				tmpFloat = tmpColor.a*tmpMat.GetFloat("_Shininess");
				tmpColor += tmpFloat*TraceRay(hit.point+hit.normal*0.0001, Vector3.Reflect(direction, hit.normal), stack+1);
			}

			if (UseLighting) {
				if (SmoothEdges) {
					tmpColor *= TraceLight(hit.point+hit.normal*0.0001, InterpolateNormal(hit.point, hit.normal, hit.collider.transform.parent.GetComponent(MeshFilter).mesh, hit.triangleIndex, hit.transform));
				}
				else {
					tmpColor *= TraceLight(hit.point+hit.normal*0.0001, hit.normal);
				}
			}
			
			tmpColor.a = 1;
			return tmpColor;
		}
		else {
			return Color.red;
		}
	}
	else {
		if (RenderSettings.skybox) {
			tmpColor = SkyboxTrace(direction, RenderSettings.skybox);
			tmpColor += Color.white*(1-tmpColor.a)/10;
			tmpColor.a = 1;
			
			return tmpColor;
		}
		else {
			return Color.blue;
		}
	}
}

function SkyboxTrace(direction:Vector3, skybox:Material):Color {
	
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

function GetMatFromTrisInMesh(mesh:Mesh, trisIndex:int):int {
	tri = [mesh.triangles[trisIndex*3], mesh.triangles[trisIndex*3+1], mesh.triangles[trisIndex*3+2]];

	for (index = 0; index < mesh.subMeshCount; index++) {
		tris = mesh.GetTriangles(index);
		for (index2 = 0; index2 < tris.length; index2 += 3) {
			if (tris[index2] == tri[0] && tris[index2+1] == tri[1] && tris[index2+2] == tri[2]) {
				return index;
			}
		}
	}
}

function InterpolateNormal(point:Vector3, normal:Vector3, mesh:Mesh, trisIndex:int, trans:Transform):Vector3 {
	index = mesh.triangles[trisIndex*3];
	index2 = mesh.triangles[trisIndex*3+1];
	index3 = mesh.triangles[trisIndex*3+2];

	var tmpIndex:int;

	var d1:float = Vector3.Distance(mesh.vertices[index], point);
	var d2:float = Vector3.Distance(mesh.vertices[index2], point);
	var d3:float = Vector3.Distance(mesh.vertices[index3], point);

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

	var plane:Plane = Plane(trans.TransformPoint(mesh.vertices[index2]), trans.TransformPoint(mesh.vertices[index3])+normal, trans.TransformPoint(mesh.vertices[index3])-normal);
	ray = Ray(trans.TransformPoint(mesh.vertices[index]), (point - trans.TransformPoint(mesh.vertices[index])).normalized);

	if (!plane.Raycast(ray, tmpFloat)) {
		return normal;
	}

	var point2:Vector3 = ray.origin+ray.direction*tmpFloat;
	var normal2:Vector3 = Vector3.Lerp(trans.TransformDirection(mesh.normals[index2]), trans.TransformDirection(mesh.normals[index3]), Vector3.Distance(trans.TransformPoint(mesh.vertices[index2]), point2)/Vector3.Distance(trans.TransformPoint(mesh.vertices[index2]), trans.TransformPoint(mesh.vertices[index3])));
	var normal3:Vector3 = Vector3.Lerp(normal2, trans.TransformDirection(mesh.normals[index]), Vector3.Distance(point2, point)/Vector3.Distance(point2, trans.TransformPoint(mesh.vertices[index])));
	return normal3;
}

function TraceLight(pos:Vector3, normal:Vector3):Color {

	var tmpColor:Color = RenderSettings.ambientLight;

	for (light in lights) {

		if (light.enabled) {
			tmpColor += LightTrace(light, pos, normal);
		}
	}

	return tmpColor;
}

function LightTrace(light:Light, pos:Vector3, normal:Vector3):Color {
	var hit:RaycastHit;

	if (light.type == LightType.Directional) {
		direction = light.transform.TransformDirection(Vector3.back);
		return transparancyTrace(Color(light.intensity, light.intensity, light.intensity)*(1-Quaternion.Angle(Quaternion.identity, Quaternion.FromToRotation(normal, direction))/90), pos, direction, Mathf.Infinity);
	}

	if (light.type == LightType.Point) {
		if (Vector3.Distance(pos, light.transform.position) <= light.range) {
			direction = (light.transform.position - pos).normalized;
			tmpFloat = (light.range-Vector3.Distance(pos, light.transform.position))/light.range*light.intensity;
			return transparancyTrace(Color(tmpFloat, tmpFloat, tmpFloat)*(1-Quaternion.Angle(Quaternion.identity, Quaternion.FromToRotation(normal, direction))/90), pos, direction, Vector3.Distance(light.transform.position, pos));
		}
	}

	if (light.type == LightType.Spot) {
		if (Vector3.Distance(pos, light.transform.position) <= light.range) {
			direction = (light.transform.position - pos).normalized;
			if (Vector3.Angle(direction, -light.transform.forward) < light.spotAngle) {
				tmpFloat = (light.range-Vector3.Distance(pos, light.transform.position))/light.range*light.intensity;
				tmpFloat *= 1 - Vector3.Angle(direction, -light.transform.forward)/light.spotAngle;
				return transparancyTrace(Color(tmpFloat, tmpFloat, tmpFloat)*(1-Quaternion.Angle(Quaternion.identity, Quaternion.FromToRotation(normal, direction))/90), pos, direction, Vector3.Distance(light.transform.position, pos));
			}
		}
	}

	return Color.black;
}


function transparancyTrace(col:Color, pos:Vector3, dir:Vector3, dist:float) {
	var tmpColor = col;
	var hits:RaycastHit[];
	var hit:RaycastHit;

	hits = Physics.RaycastAll(pos, dir, dist, collisionMask);

	for (hit in hits) {

		if (hit.collider.transform.parent.GetComponent(MeshFilter).mesh.subMeshCount > 1 && !SingleMaterialOnly) {
			tmpMat = hit.collider.transform.parent.GetComponent.<Renderer>().materials[GetMatFromTrisInMesh(hit.collider.transform.parent.GetComponent(MeshFilter).mesh, hit.triangleIndex)];
		}
		else {
			tmpMat = hit.collider.transform.parent.GetComponent.<Renderer>().material;
		}

		if (tmpMat.mainTexture) {
			tmpTex = (tmpMat.mainTexture as Texture2D);
			tmpColor *= 1-tmpTex.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y).a;
		}
		else {
			tmpColor *= 1-tmpMat.color.a;
		}
	}

	return tmpColor;
}

function SaveTextureToFile(texture:Texture2D, fileName):void {
	var bytes = texture.EncodeToPNG();
	var file = new File.Open(Application.dataPath + "/" + fileName,FileMode.Create);
	BinaryWriter(file).Write(bytes);
	file.Close();
}

function GenerateColliders(go:GameObject):GameObject {

	if (go.GetComponent(MeshFilter)) {
		GenerateColliders(go.GetComponent(MeshFilter));
	}

	return go;
}


function GenerateColliders(mf:MeshFilter):GameObject {

	tmpGameObject = GameObject("MeshRender");

	tmpGameObject.transform.parent = mf.transform;
	tmpGameObject.AddComponent(MeshFilter).mesh = mf.mesh;
	tmpGameObject.AddComponent(MeshCollider).sharedMesh = mf.mesh;

	tmpGameObject.transform.localPosition = Vector3.zero;
	tmpGameObject.transform.localScale = Vector3.one;
	tmpGameObject.transform.rotation = mf.transform.rotation;

	tmpGameObject.layer = 31;
	return tmpGameObject;
}