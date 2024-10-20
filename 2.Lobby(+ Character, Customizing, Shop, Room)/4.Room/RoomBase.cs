using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomBase : MonoBehaviour
{
    /*캐릭터와 로딩 이미지를 저장*/
    public GameObject[] characterPrefabs;
    public Sprite[] GloadList;

    private Camera cam;

    void Start()
    {   
        cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        AdjustFOV();
    }
    /*방에서도 좌우 유저 캐릭터가 비율에 따라 잘리는 경우를 방지하기 위해 카메라조정*/
    void AdjustFOV()
    {
        float aspectRatio = Screen.width / (float)Screen.height;

        if (aspectRatio > 1.7) // 16:9
        {
            cam.fieldOfView = 45;
        }
        else if (aspectRatio > 1.5) // 16:10
        {
            cam.fieldOfView = 49;
        }
        else if (aspectRatio <= 1.5 && aspectRatio > 1.3) // 4:3
        {
            cam.fieldOfView = 56;
        }
        else if (aspectRatio <= 1.3 && aspectRatio > 1.2) // 5:4
        {
            cam.fieldOfView = 56;
        }
    }
}
