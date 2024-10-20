using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using System;
using TMPro;

public class UnitsSelection_Net : MonoBehaviour
{
    private bool _isDraggingMouseBox = false;
    private Vector3 _dragStartPosition;
    
    private const float _samplingRange = 5f;
    private const float _samplingRadius = 2.5f;

    Ray _ray;
    RaycastHit _raycastHit;

    private List<Vector3> targetPositions;
    System.Random rnd = new System.Random();

    private bool A_movebool = false;
    public Transform Attackenemytarget;

    private Dictionary<int, List<UnitManager_Net>> _selectionGroups = new Dictionary<int, List<UnitManager_Net>>();
    int PlayerlayerMask = (1 << 8);

    private Player playerScript;
    string playerTag;

    public TMP_InputField chatInputField;

    private float lastClickTime = 0f;
    KeyCode lastKeyCode;
    public CameraCon camcon1;

    public InGameSoundManager IngamesoundManager;

    public Texture2D cursorMain;
    public Texture2D cursorAttack;
    public Texture2D[] cursorUnit;
    public Texture2D[] cursorEnemy;
    private Coroutine cursorCoroutine;
    private Texture2D currentCursor;
    public GameObject MoveMarker;
    public GameObject AttackMarker;

    void Start()
    {
        playerScript = GetComponent<Player>();
        
        if (playerScript == null)
        {
            Debug.LogError("Player script not found on this GameObject!");
        }
        playerTag = "Unit_p" + (playerScript.playerID + 1);
        
        IngamesoundManager = GameObject.Find("InGame_SoundObject").GetComponent<InGameSoundManager>();
    }

    private void Update()
    {
        /*A키 클릭을 안한 상태에서 마우스 좌클릭 시, UI 위에서 클릭 불가능*/
        if (Input.GetMouseButtonDown(0) && A_movebool == false && !IsPointerOverUI())    
        {
            /*드래그 박스를 true로 변경하여 박스 선택이 가능하도록 함*/
            _isDraggingMouseBox = true;
            _dragStartPosition = Input.mousePosition;
        }
        /*좌클릭 Up 시 박스 해제*/
        if (Input.GetMouseButtonUp(0) && A_movebool == false)
            _isDraggingMouseBox = false;

        /*드래그 박스는 shift키를 누르고 있는지에 따라 구분*/
        if(!Input.GetKey(KeyCode.LeftShift))
        {
            if (_isDraggingMouseBox && _dragStartPosition != Input.mousePosition)
                _SelectUnitsInDraggingBox();
        }
        else if(Input.GetKey(KeyCode.LeftShift))
        {
            if (_isDraggingMouseBox && _dragStartPosition != Input.mousePosition)
                _SelectUnitsInDraggingBox_Shift();
        }
        
        if (Globals_Net.SELECTED_UNITS.Count > 0)
        {
            /*선택 유닛이 있는데 esc 키 누를 경우 유닛 선택 취소*/
            if (Input.GetKeyDown(KeyCode.Escape))
                _DeselectAllUnits();
            /*유닛이 없는 땅 선택 시 모든 유닛 선택 취소*/
            if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftShift) && A_movebool == false && !IsPointerOverUI())
            {
                _ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(
                    _ray,
                    out _raycastHit,
                    1000f
                ))
                {
                    if (_raycastHit.transform.tag == "Terrain")
                    {
                        _DeselectAllUnits();
                    }
                }
            }
        }
        if (Input.anyKeyDown)
        {
            /*Ctrl + 숫자로 현재 선택 유닛 부대 지정*/
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
            {
                KeyCode[] keyCodes = {
                    KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
                    KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
                };
                
                for (int i = 0; i < keyCodes.Length; i++)
                {
                    if (Input.GetKeyDown(keyCodes[i]))
                    {
                        _CreateSelectionGroup(i);
                        break;
                    }
                }
            }
            /*숫자키로 지정한 부대 선택 가능*/
            else
            {
                KeyCode[] keyCodes = {
                    KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
                    KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
                };
                
                for (int i = 0; i < keyCodes.Length; i++)
                {
                    if (Input.GetKeyDown(keyCodes[i]) && chatInputField != null && !chatInputField.isFocused)
                    {
                        float currentTime = Time.time;
                        if (lastKeyCode == keyCodes[i] && (currentTime - lastClickTime < 0.3f))
                        {
                            _ReselectGroupDoubleClick(i);
                        }
                        else
                        {
                            _ReselectGroup(i);
                        }
                        lastKeyCode = keyCodes[i];
                        lastClickTime = currentTime;
                        break;
                    }
                }
            }
        }
        /*유닛 선택 후 우클릭 시 이동*/
        if (Globals_Net.SELECTED_UNITS.Count > 0 && Input.GetMouseButtonUp(1) && A_movebool == false)
        {
            _ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(
                _ray,
                out _raycastHit,
                1000f,
                PlayerlayerMask
            ))
            {
                /*이동 마크 생성*/
                GameObject moveMarkerInstance = Instantiate(MoveMarker, _raycastHit.point, Quaternion.identity);
                Destroy(moveMarkerInstance, 1f);
                foreach (UnitManager_Net um in Globals_Net.SELECTED_UNITS)
                {
                    /*유닛이 하나라도 있으면 PoissonDiscSampling을 유닛의 반지름으로 지정해 위치를 선정
                    aipath가 동시에 한 위치로 이동할 때 목적지 근처에서 멈추지 못하는 것을 막기 위함*/
                    if(um.SelectIndex ==0)
                    {
                        targetPositions = _ComputeFormationTargetPositions(_raycastHit.point);
                    }
                    /*targetPostions이 존재하면 targetpostions에서 랜덤한 위치로 이동*/
                    if(targetPositions != null)
                    {
                        um.StopDone();
                        int randIndex = rnd.Next(targetPositions.Count);
                        um.MoveTo(targetPositions[randIndex]);
                    }
                }
            }
        }

        /*A 클릭 시 공격 활성화*/
        if (Globals_Net.SELECTED_UNITS.Count > 0 && Input.GetKeyDown(KeyCode.A) && chatInputField != null && !chatInputField.isFocused)
        {
            A_movebool = true;
            if(IngamesoundManager != null)
                IngamesoundManager.UnitClickSound();
        }

        if (Globals_Net.SELECTED_UNITS.Count > 0 && A_movebool == true)
        {
            if(Input.GetMouseButtonDown(0))
            {
                _ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(
                    _ray,
                    out _raycastHit,
                    1000f
                ))
                {
                    /*공격 대상을 마우스로 클릭한 경우*/
                    if(_raycastHit.transform.tag == "Enemy")
                    {
                        Attackenemytarget = _raycastHit.transform;
                        /*적 지정 공격을 진행*/
                        foreach (UnitManager_Net um in Globals_Net.SELECTED_UNITS)
                        {
                            um.StopDone();
                            um.MoveAttackToEnemy(Attackenemytarget);
                        }
                    }
                    /*공격 대상이 없는 경우*/
                    else
                    {
                        /*목적지 공격을 진행*/
                        GameObject moveMarkerInstance2 = Instantiate(AttackMarker, _raycastHit.point, Quaternion.identity);
                        Destroy(moveMarkerInstance2, 1f);
                        foreach (UnitManager_Net um in Globals_Net.SELECTED_UNITS)
                        {
                            if(um.SelectIndex ==0)
                            {
                                targetPositions = _ComputeFormationTargetPositions(_raycastHit.point);
                            }
                    
                            if(targetPositions != null)
                            {
                                um.StopDone();
                                int randIndex = rnd.Next(targetPositions.Count);
                                um.MoveAttackTo(targetPositions[randIndex]);
                            }
                        }
                    }
                }
                A_movebool = false;
            }
            /*우클릭 시 공격 비활성화*/
            if(Input.GetMouseButtonUp(1))
            {
                A_movebool = false;
            }
            
        }
        /*S버튼 클릭 시 정지, 공격 비활성화*/
        if (Globals_Net.SELECTED_UNITS.Count > 0 && Input.GetKeyDown(KeyCode.S) && chatInputField != null && !chatInputField.isFocused)
        {
            foreach (UnitManager_Net um in Globals_Net.SELECTED_UNITS)
            {
                um.StopTo();
                um.AttFalse();
            }
            if(IngamesoundManager != null)
                IngamesoundManager.UnitClickSound();
        }
        /*H버튼 클릭 시 정지, 공격 활성화*/
        if (Globals_Net.SELECTED_UNITS.Count > 0 && Input.GetKeyDown(KeyCode.H) && chatInputField != null && !chatInputField.isFocused)
        {
            foreach (UnitManager_Net um in Globals_Net.SELECTED_UNITS)
            {
                um.StopTo();
                um.AttTrue();
            }
            if(IngamesoundManager != null)
                IngamesoundManager.UnitClickSound();
        }
        /*커서 변경*/
        if (A_movebool)
        {
            _ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(_ray, out _raycastHit, 1000f))
            {
                /*공격 활성화 + 마우스가 적의 위치에 있을 때 애니메이션 진행*/
                if (_raycastHit.transform.tag == "Enemy")
                {
                    SetCursor(cursorEnemy, 120f);
                }
                /*공격 활성화 + 마우스가 적의 위치에 없을 때는 붉은 커서로 공격 활성화 시각화*/
                else
                {
                    SetCursor(cursorAttack);
                }
            }
            else
            {
                SetCursor(cursorAttack);
            }
        }
        else
        {
            _ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(_ray, out _raycastHit, 1000f))
            {
                /*공격이 비활성화고 내 유닛 위치에 커서가 있을 경우 애니메이션 진행*/
                if (_raycastHit.transform.tag == playerTag)
                {
                    SetCursor(cursorUnit, 120f);
                }
                /*아닌 경우는 기본 커서*/
                else
                {
                    SetCursor(cursorMain);
                }
            }
            else
            {
                SetCursor(cursorMain);
            }
        }
    }

    private List<Vector2> _ComputeFormationTargetOffsets()
    {
        int nSelectedUnits = Globals_Net.SELECTED_UNITS.Count;
        List<Vector2> offsets = new List<Vector2>(nSelectedUnits);
        offsets.Add(Vector2.zero);
        if (nSelectedUnits == 1)
            return offsets;

        offsets.AddRange(Utils.SampleOffsets(
            nSelectedUnits - 1, _samplingRadius, _samplingRange * Vector2.one));
        return offsets;
    }

    private List<Vector3> _ComputeFormationTargetPositions(Vector3 hitPoint)
    {
        int nSelectedUnits = Globals_Net.SELECTED_UNITS.Count;
        List<Vector3> positions = new List<Vector3>(nSelectedUnits);
        positions.Add(hitPoint);
        /*유닛이 하나인 경우는 정확히 그 위치로 가기 위함*/
        if (nSelectedUnits == 1)
            return positions;
        /*유닛 개수, 유닛 반지름, 반경으로 포아송 진행*/
        positions.AddRange(Utils.SamplePositions(
            nSelectedUnits - 1, _samplingRadius,
            _samplingRange * Vector2.one, hitPoint));
        return positions;
    }
    
    /*최상위에 드래그 박스 표시를 위함*/
    void OnGUI()
    {
        if (_isDraggingMouseBox)
        {
            var rect = Utils.GetScreenRect(_dragStartPosition, Input.mousePosition);
            Utils.DrawScreenRect(rect, new Color(1.0f, 1.0f, 1.0f, 0.1f));
            Utils.DrawScreenRectBorder(rect, 3, new Color(0.5f, 1f, 0.4f));
        }
    }

    /*유닛 그룹 선택*/
    public void SelectUnitsGroup(int groupIndex)
    {
        _ReselectGroup(groupIndex);
    }
    /*shift 없는 드래그 박스로 유닛 선택*/
    private void _SelectUnitsInDraggingBox()
    {
        Bounds selectionBounds = Utils.GetViewportBounds(
            Camera.main,
            _dragStartPosition,
            Input.mousePosition
        );
        GameObject[] selectableUnits = GameObject.FindGameObjectsWithTag(playerTag);
        bool inBounds;
        foreach (GameObject unit in selectableUnits)
        {
            inBounds = selectionBounds.Contains(
                Camera.main.WorldToViewportPoint(unit.transform.position)
            );
            /*박스 안은 select, 박스 밖은 deselect*/
            if (inBounds)
                unit.GetComponent<UnitManager_Net>().Select();
            else
                unit.GetComponent<UnitManager_Net>().Deselect();
        }
    }
    /*shift + 드래그 박스 선택*/
    private void _SelectUnitsInDraggingBox_Shift()
    {
        List<UnitManager_Net> selectedUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
        foreach (UnitManager_Net um in selectedUnits)
            um.Select();
        Bounds selectionBounds = Utils.GetViewportBounds(
            Camera.main,
            _dragStartPosition,
            Input.mousePosition
        );
        GameObject[] selectableUnits = GameObject.FindGameObjectsWithTag(playerTag);
        bool inBounds;
        foreach (GameObject unit in selectableUnits)
        {
            inBounds = selectionBounds.Contains(
                Camera.main.WorldToViewportPoint(unit.transform.position)
            );
            /*Deselet 없이 Select만 진행*/
            if (inBounds)
                unit.GetComponent<UnitManager_Net>().Select();
        }
    }
    /*모든 유닛 선택 해제*/
    private void _DeselectAllUnits()
    {
        List<UnitManager_Net> selectedUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
        foreach (UnitManager_Net um in selectedUnits)
            um.Deselect();
    }
    /*유닛 부대 지정*/
    private void _CreateSelectionGroup(int groupIndex)
    {
        if (Globals_Net.SELECTED_UNITS.Count == 0)
        {
            if (_selectionGroups.ContainsKey(groupIndex))
                _RemoveSelectionGroup(groupIndex);
            return;
        }
        List<UnitManager_Net> groupUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
        _selectionGroups[groupIndex] = groupUnits;
    }
    /*기조 부대 지정 지우기*/
    private void _RemoveSelectionGroup(int groupIndex)
    {
        _selectionGroups.Remove(groupIndex);
    }
    /*부대 키를 두번 누를 때 해당 위치로 카메라 전환을 위함*/
    private void _ReselectGroupDoubleClick(int groupIndex)
    {
        if (!_selectionGroups.ContainsKey(groupIndex)) return;
        if (_selectionGroups[groupIndex].Count > 0)
        {
            /*선택 유닛의 첫번째 INDEX 유닛의 위치로 화면 전환*/
            UnitManager_Net firstUnit = _selectionGroups[groupIndex][0];
            camcon1.SetTargetPosition(firstUnit.transform);
        }
        
    }
    /*기존 선택을 해제하고 부대 지정한 그룹 선택*/
    private void _ReselectGroup(int groupIndex)
    {
        if (!_selectionGroups.ContainsKey(groupIndex)) return;
        _DeselectAllUnits();
        foreach (UnitManager_Net um in _selectionGroups[groupIndex])
            um.Select();
    }
    
    private bool IsPointerOverUI()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }
    
    /*커서 애니메이션은 텍스처를 시간에 따라 변경하는 것으로 진행*/
    private IEnumerator AnimateCursor(Texture2D[] cursorTextures, float frameRate)
    {
        int index = 0;
        while (true)
        {
            Cursor.SetCursor(cursorTextures[index], Vector2.zero, CursorMode.Auto);
            index = (index + 1) % cursorTextures.Length;
            yield return new WaitForSeconds(1f / frameRate);
        }
    }
    /*현재 커서와 새로운 커서가 다를 경우, 커서 코루틴이 진행 중이면 종료하고 새 커서 업데이트*/
    private void SetCursor(Texture2D cursorTexture)
    {
        if (cursorTexture != currentCursor)
        {
            if (cursorCoroutine != null)
            {
                StopCoroutine(cursorCoroutine);
                cursorCoroutine = null;
            }
            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
            currentCursor = cursorTexture;
        }
    }
    /*현재 커서와 새로운 커서가 다를 경우, 커서 코루틴이 진행 중이면 종료하고 새 커서 업데이트*/
    private void SetCursor(Texture2D[] cursorTextures, float frameRate)
    {
        if (cursorTextures.Length > 0 && cursorTextures[0] != currentCursor)
        {
            if (cursorCoroutine != null)
            {
                StopCoroutine(cursorCoroutine);
            }
            cursorCoroutine = StartCoroutine(AnimateCursor(cursorTextures, frameRate));
            currentCursor = cursorTextures[0];
        }
    }

}
