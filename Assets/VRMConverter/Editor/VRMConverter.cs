using CustomAvatar;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VRMConverter : Editor
{
    private static List<GameObject> FindChildren(Transform parent)
    {
        List<GameObject> children = new List<GameObject>
        {
            parent.gameObject
        };

        // Check if the parent has children
        if (parent.childCount > 0)
        {
            // Iterate through each child of the parent
            foreach (Transform childTransform in parent)
            {
                // Recursively find and print children of the child
                var tmp = FindChildren(childTransform);
                children.AddRange(tmp);
            }
        }

        return children;
    }

    private static Vector3 FindRelativePosition(List<GameObject> objs, string identifier)
    {
        GameObject obj = objs.Find(tmp => tmp.name.ToLower().EndsWith(identifier));
        if (obj != null)
        {
            return obj.transform.position;
        }

        return Vector3.zero;
    }


    [MenuItem("VRMConverter/Cleanup Missing Scripts")]
    static void CleanupMissingScripts()
    {
        //Get the current scene and all top-level GameObjects in the scene hierarchy
        Scene currentScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = currentScene.GetRootGameObjects();

        foreach (GameObject g in rootObjects)
        {
            List<GameObject> childObjects = FindChildren(g.transform);
            childObjects.Add(g);

            foreach (GameObject obj in childObjects)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            }
        }
    }


    [MenuItem("VRMConverter/Prepare")]
    static void PrepareVRM()
    {
        GameObject vrmModel = Selection.activeGameObject;

        GameObject root = new GameObject(vrmModel.name + "[VRMC]");
        AvatarDescriptor avatarDescriptor = root.AddComponent<AvatarDescriptor>();
        vrmModel.transform.parent = root.transform;
        vrmModel.transform.localPosition = Vector3.zero;

        List<GameObject> objects = FindChildren(root.transform);

        GameObject body = new GameObject("Body");
        body.transform.parent = root.transform;
        GameObject head = new GameObject("Head");
        head.transform.parent = root.transform;
        head.transform.position = FindRelativePosition(objects, "_head");

        GameObject headTarget = new GameObject("HeadTarget");
        headTarget.transform.parent = head.transform;

        GameObject leftHand = new GameObject("LeftHand");
        leftHand.transform.parent = root.transform;
        leftHand.transform.position = FindRelativePosition(objects, "l_hand");
        GameObject rightHand = new GameObject("RightHand");
        rightHand.transform.parent = root.transform;
        rightHand.transform.position = FindRelativePosition(objects, "r_hand");

        GameObject pelvis = new GameObject("Pelvis");
        pelvis.transform.parent = root.transform;
        pelvis.transform.position = FindRelativePosition(objects, "_Hips");

        GameObject leftLeg = new GameObject("LeftLeg");
        leftLeg.transform.parent = root.transform;
        leftLeg.transform.position = FindRelativePosition(objects, "l_foot");
        GameObject rightLeg = new GameObject("RightLeg");
        rightLeg.transform.parent = root.transform;
        rightLeg.transform.position = FindRelativePosition(objects, "r_foot");

        VRIKManager vRIKManager = vrmModel.AddComponent<VRIKManager>();
        vRIKManager.solver_spine_headTarget = headTarget.transform;
        vRIKManager.solver_spine_pelvisTarget = pelvis.transform;
        vRIKManager.solver_leftLeg_target = leftLeg.transform;
        vRIKManager.solver_rightLeg_target = rightLeg.transform;

        PoseManager poseManager = vrmModel.AddComponent<PoseManager>();

        // Setup Dynamic Bones
        // Root of bones is called Root?
        Transform boneRoot = vrmModel.transform.Find("Root");
        if (boneRoot != null)
        {
            List<GameObject> boneObjects = FindChildren(boneRoot);
            foreach (GameObject boneObject in boneObjects)
            {
                boneObject.AddComponent<DynamicBone>();
            }
        }
        else
        {
            Debug.LogError("Could not find bone root. (Name = 'Root')");
        }
    }


    [MenuItem("VRMConverter/Replace Shaders")]
    static void ReplaceShaders()
    {
        GameObject root = Selection.activeGameObject;
        List<GameObject> objects = FindChildren(root.transform);

        foreach (GameObject obj in objects)
        {
            Renderer renderer = obj.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material[] materials = renderer.sharedMaterials;

                foreach (Material material in materials)
                {
                    if (!IsBeatSaberShader(material.shader))
                    {
                        Shader transparentShader = Shader.Find("BeatSaver/Transparent");

                        string targetShader = "BeatSaber/CellShading_Wnormals";
                        Shader newShaderObject = Shader.Find(targetShader);

                        if (newShaderObject != null)
                        {
                            Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>> shaderProps = GetShaderProperties(material);
#if false
                            Debug.Log("==========================   BEFORE  ==========================");
                            foreach (var item in shaderProps)
                            {
                                Debug.Log(item.Key + ": " + item.Value.Item2);
                            }
#endif

                            shaderProps = MapShaderProperties(shaderProps, targetShader);

                            material.shader = newShaderObject;
                            if (shaderProps.ContainsKey("_Color"))
                            {
                                Debug.LogError(((Color)shaderProps["_Color"].Item2).a);
                                if (((Color) shaderProps["_Color"].Item2).a == 0f)
                                {
                                    material.shader = transparentShader;
                                }
                            }


                            SetShaderProperties(material, shaderProps);

                            shaderProps = GetShaderProperties(material);
#if false
                            Debug.Log("==========================   AFTER  ==========================");
                            foreach (var item in shaderProps)
                            {
                                Debug.Log(item.Key + ": " + item.Value.Item2);
                            }
#endif

                        }
                        else
                        {
                            Debug.LogError("Failed to find the new shader: " +targetShader);
                        }
                    }
                }
            }
        }

    }

    private static Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>> MapShaderProperties(Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>> shaderProps, string targetShader)
    {
        Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>> result = new Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>>();

        if (targetShader == "BeatSaber/CellShading_Wnormals")
        {
            foreach (var item in shaderProps)
            {
                switch (item.Key)
                {
                    case "_MainTex":
                        result["_Tex"] = item.Value; break;
                    case "_BumpMap":
                        result["_NormalMap"] = item.Value; break;
                    default:
                        result[item.Key] = item.Value; break;
                }
            }
        } else
        {
            result = shaderProps;
        }


        return result;
    }

    private static void SetShaderProperties(Material material, Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>> shaderProps)
    {
        foreach (var item in shaderProps)
        {
            switch (item.Value.Item1)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    material.SetColor(item.Key, (Color)item.Value.Item2);
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                    material.SetFloat(item.Key, (float)item.Value.Item2);
                    break;
                case ShaderUtil.ShaderPropertyType.Range:
                    material.SetFloat(item.Key, (float)item.Value.Item2);
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    material.SetVector(item.Key, (Vector4)item.Value.Item2);
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    material.SetTexture(item.Key, (Texture)item.Value.Item2);
                    break;
                default:
                    Debug.LogError("Type is not implemented: " + item.Value.Item1);
                    break;
            }
        }
    }

    private static Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>> GetShaderProperties(Material material)
    {
        Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>> result = new Dictionary<string, Tuple<ShaderUtil.ShaderPropertyType, object>>();

        int propertyCount = ShaderUtil.GetPropertyCount(material.shader);
        for (int i = 0; i < propertyCount; i++)
        {
            string propertyName = ShaderUtil.GetPropertyName(material.shader, i);
            ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(material.shader, i);


            Tuple<ShaderUtil.ShaderPropertyType, object> value = null;
            int propertyID = Shader.PropertyToID(propertyName);
            switch (propertyType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    Color colorValue = material.GetColor(propertyID);
                    value = new Tuple<ShaderUtil.ShaderPropertyType, object>(propertyType, colorValue);
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                    float floatValue = material.GetFloat(propertyID);
                    value = new Tuple<ShaderUtil.ShaderPropertyType, object>(propertyType, floatValue);
                    break;
                case ShaderUtil.ShaderPropertyType.Range:
                    float rangeValue = material.GetFloat(propertyID);
                    value = new Tuple<ShaderUtil.ShaderPropertyType, object>(propertyType, rangeValue);
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    Vector4 vector = material.GetVector(propertyID);
                    value = new Tuple<ShaderUtil.ShaderPropertyType, object>(propertyType, vector);
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    Texture texEnv = material.GetTexture(propertyID);
                    value = new Tuple<ShaderUtil.ShaderPropertyType, object>(propertyType, texEnv);
                    break;
                default:
                    Debug.LogError("Type is not implemented: " + propertyType);
                    value = new Tuple<ShaderUtil.ShaderPropertyType, object>(propertyType, null);
                    break;
            }

            result.Add(propertyName, value);
        }

        return result;
    }

    private static bool IsBeatSaberShader(Shader shader)
    {
        if (shader == null)
        {
            return false;
        }

        Debug.Log(shader.name);
        if (shader.name.Contains("BeatSaber/"))
        {
            return true;
        }

        return false;
    }
}
