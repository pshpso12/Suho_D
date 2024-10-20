using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Pathfinding;
using Pathfinding.RVO;
using Mirror;

[System.Serializable]
public class ChaSkill
{
    public string name;
    public string description;
    public Sprite image;
    public float[] probabilities;
    public float[] attackPowers;
    public float DivNum = 1;
}

[RequireComponent(typeof(BoxCollider))]
public class UnitManager_Net : NetworkBehaviour
{
    public GameObject Sel_p;
    
    public AIPath aiPath;

    public Transform enemytarget;
    public float AttackRange;
    public string enemyTag = "Enemy";
    public Transform partToRotate;
    public float turnSpeed =  10f;
    public bool Attakbooler = false;
    public bool AttakEnemybooler = false;

    public Transform Attackenemytarget_Manager;

    private int _selectIndex = -1;
    public int SelectIndex { get => _selectIndex; }

    public Animator playerAnimator;
    public WeaponEvent weaponevent;
    public GameObject Unit_Weapon1;
    public GameObject Unit_Weapon2;
    [SerializeField] private RVOController rvo;
    [SerializeField] private NavmeshCut navmeshCut;
    public IEnemy enemyScript;
    public float projectileSpeed;

    private static float lastClickTime = 0f;
    private const float doubleClickTime = 0.3f;
    private static UnitManager_Net lastClickedUnit = null;

    public string MyTag;
    public int Unit_Index;
    public float soundCooldown = 3f;
    private bool canPlaySound = true;

    [SyncVar]
    public int UnitLevel;
    [SyncVar]
    public float UnitCurrentExp;
    [SyncVar]
    public float UnitMaxExp;
    [SyncVar]
    public int UnitUpgradeValue;

    [SyncVar]
    public float Buff_Power;
    [SyncVar]
    public float Buff_Speed;

    public float lastSpeed = 1;
    
    public List<ChaSkill> skills = new List<ChaSkill>();
    public int[] upgradeA_Num_ = {0, 10, 30, 55, 80, 120, 160, 200, 245, 295, 345, 400, 460, 530, 600, 680, 760, 900, 1100, 1400, 1800, 2300, 2900, 3500, 4200, 4900, 5700, 6500, 7400, 8300, 9200};
    public int[] upgradeS_Num_ = {0, 2, 4, 6, 8, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100, 115, 130, 145, 160, 180, 200, 230, 260, 290};
    public Upgrade_Fam upgrade_fam;

    public readonly SyncDictionary<string, int> Buff_Count = new SyncDictionary<string, int>();

    public GameObject LevelUpEf_;
    public GameObject PortalEf_;
    public GameObject SpawnEf_;

    void Start()
    {
        weaponevent.MagicWeapon.SetFloat("_Dissamount", 1);
        if(isServer)
        {
            /*서버 측에서 공격 타겟을 가장 가까운 적으로 0.04초 마다 체크*/
            InvokeRepeating("UpdateTarget", 0f, 0.04f);
            GameObject upgradeFamObject = GameObject.Find("M_cc_ex");
            if (upgradeFamObject != null)
            {
                upgrade_fam = upgradeFamObject.GetComponent<Upgrade_Fam>();
            }
        }
        if(isClient)
        {
            GameObject PlayerMC = GameObject.Find("M_cc_ex");
            Player Forindex = PlayerMC.GetComponent<Player>();
            MyTag = "Unit_p" + (Forindex.playerID + 1);
            aiPath.enabled = false;
        }
    }
    void UpdateTarget ()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float shortestDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;
        /*공격할 적을 지정하지 않은 경우 가장 가까운 적을 지정*/
        if(AttakEnemybooler == false)
        {
            foreach (GameObject enemy in enemies)
            {
                float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
                if(distanceToEnemy < shortestDistance)
                {
                    shortestDistance = distanceToEnemy;
                    nearestEnemy = enemy;
                }
            }
        }
        /*공격할 적을 지정한 경우, 해당 적이 존재하고 Tag가 적이 맞을 경우, 해당 적을 지정*/
        if(AttakEnemybooler == true && Attackenemytarget_Manager != null && Attackenemytarget_Manager.gameObject.tag == enemyTag)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, Attackenemytarget_Manager.position);
            if(distanceToEnemy <= AttackRange)
            {
                shortestDistance = AttackRange;
                nearestEnemy = Attackenemytarget_Manager.gameObject;
            }
        }
        /*해당 적이 공격 범위 내에 있을 경우 공격할 수 있는 것으로 지정*/
        if(nearestEnemy !=null && shortestDistance <= AttackRange)
        {
            enemytarget = nearestEnemy.transform;
            enemyScript = enemytarget.GetComponent<IEnemy>();
        }
        /*그 외의 경우 idle 애니메이션을 진행하기 위해 초기화*/
        else
        {
            enemytarget = null;
            enemyScript = null;
        }
    }

    void FixedUpdate (){
        if(!isServer)
            return;
        /*타겟이 존재하지 않거나, 공격 비활성화, 유닛이 움직이고 있는 경우*/
        if(enemytarget == null || Attakbooler == false || aiPath.isStopped == false)
        {
            /*유닛 회전을 가능하게 하고 애니메이션 Attack 값을 변경해, idle, move 중 하나를 진행할 수 있게 변경*/
            aiPath.enableRotation = true;
            playerAnimator.SetBool("Attack", false);
            return;
        }
        /*적이 있고 유닛이 멈춰있고 공격이 활성화된 경우*/
        if(enemytarget != null && aiPath.isStopped == true && Attakbooler == true)
        {
            /*유닛 기준으로 속도를, 발사체의 속도와 현재나의 위치와 적의 위치를 통해, 발사체 예측 위치를 지정*/
            Vector3 enemyVelocity = enemyScript.enemySpeed * enemytarget.forward;
            float timeToReachTarget = Vector3.Distance(transform.position, enemytarget.position) / projectileSpeed;
            Vector3 predictedPosition = enemytarget.position + enemyVelocity * (timeToReachTarget);

            /*공격 시 움직임에 의한 회전을 막음*/
            aiPath.enableRotation = false;

            /*유닛이 예측 위치를 Lerp하게 보도록 지정*/
            Vector3 Atargetdir = predictedPosition - transform.position;
            Quaternion lookRotation = Quaternion.LookRotation(Atargetdir);
            Vector3 Atargetrotation = Quaternion.Lerp(partToRotate.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;
            partToRotate.rotation = Quaternion.Euler(0f, Atargetrotation.y, 0f);
            /*Attack을 true로 지정하여 공격 진행*/
            playerAnimator.SetBool("Attack", true);
        }

        /*공격 애니메이션의 진행 속도를 공격속도에 따라 변경하기 위함
         절대값을 이용해 값이 변경된 경우 Skill_Speed를 변경할 수 있게 함(추후, 공격속도 상한선을 지정할 예정)*/
        float currentSpeed = CalculateCurrentSpeed();
        if (Mathf.Abs(lastSpeed - currentSpeed) > Mathf.Epsilon) 
        {
            lastSpeed = currentSpeed;
            playerAnimator.SetFloat("Skill_Speed", lastSpeed);
        }
    }
    /*개발 단계에서 공격범위 확인*/
    void OnDrawGizmosSelected(){
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
    
    private void OnMouseDown()
    {
       if (IsActive())
             /*캐릭터 더블 클릭 시 동일한 유닛 다중 선택*/
            if (Time.time - lastClickTime < doubleClickTime && lastClickedUnit == this)
            {
                SelectAllSimilarUnits();
            }
            /*Shift와 마우스 클릭 시 기본 선택 목록에 클릭한 유닛 추가하기 위함*/
            else
            {
                Select(
                    true,
                    Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift)
                );
                lastClickedUnit = this;
                lastClickTime = Time.time;
            }
    }
    /*유닛 중에 이름이 동일한 유닛들 선택*/
    private void SelectAllSimilarUnits()
    {
        foreach (var unit in FindObjectsOfType<UnitManager_Net>())
        {
            if (unit.name == this.name)
            {
                unit._SelectUtil();
            }
        }
    }

    protected virtual bool IsActive()
    {
        return true;
    }

    /*유닛 선택*/
    public void _SelectUtil()
    {
        /*내 Tag가 아닌 유닛은 선택 불가*/
        if(this.gameObject.tag != MyTag) return;

        /*이미 선택한 유닛을 다시 선택 불가*/
        if (Globals_Net.SELECTED_UNITS.Contains(this)) return;
        
        /*이 유닛을 선택 목록에 추가, 선택 표시 활성화*/
        Globals_Net.SELECTED_UNITS.Add(this);
        Sel_p.SetActive(true);

        /*선택 위치는 선택목록 길이 - 1*/
        _selectIndex = Globals_Net.SELECTED_UNITS.Count - 1;

        /*유닛 선택 시 랜덤한 캐릭터 보이스 중 하나 재생*/
        if(InGameSoundManager.Instance != null && canPlaySound)
        {
            InGameSoundManager.Instance.PlayCharacterVoice(Unit_Index, Random.Range(0, 3), transform.position, 0);
            StartCoroutine(SoundCooldownCoroutine());
        }
    }
    /*캐릭터 보이스가 겹쳐서 들리는걸 막기 위함*/
    private IEnumerator SoundCooldownCoroutine()
    {
        canPlaySound = false;
        yield return new WaitForSeconds(soundCooldown);
        canPlaySound = true;
    }

    public void Select() { Select(false, false); }
    public void Select(bool singleClick, bool holdingShift)
    {
        /*singleClick이 아닌 경우(드래그 박스로 선택)인데 Shift를 안 누른 경우, 박스 안의 유닛 선택*/
        if (!singleClick && !holdingShift)
        {
            _SelectUtil();
            return;
        }
        /*Shift를 안누르고 단일 클릭 시 기본 유닛 목록을 지우고 해당 유닛만 클릭*/
        if (!holdingShift && singleClick)
        {
            List<UnitManager_Net> selectedUnits = new List<UnitManager_Net>(Globals_Net.SELECTED_UNITS);
            foreach (UnitManager_Net um in selectedUnits)
                um.Deselect();
            _SelectUtil();
        }
        /*Shift를 누르고 단일 클릭 시 해당 유닛이 없으면 목록에 추가, 있으면 목록에서 삭제*/
        if (singleClick && holdingShift)
        {
            if (!Globals_Net.SELECTED_UNITS.Contains(this))
            {
                _SelectUtil();
            }
            else
            {
                Deselect();
            }
        }
    }
    /*유닛을 선택해제*/
    public void Deselect()
    {
        if (!Globals_Net.SELECTED_UNITS.Contains(this)) return;
        Globals_Net.SELECTED_UNITS.Remove(this);
        Sel_p.SetActive(false);

        _selectIndex = -1;
    }
    /*선택 + 우클릭 시*/
    [Command]
    public void MoveTo(Vector3 targetPosition)
    {
        /*Navmesh 위에서 움직일 수 있게 aipath 활성화,
         지점을 정하고 최단 경로를 찾음*/
        aiPath.enabled = true;
        aiPath.destination = targetPosition;
        aiPath.SearchPath();
        /*그냥 Move를 찍으면, 해당 위치에 이동할 때 까지 공격을 안하기 위해 공격기능 비활성화*/
        Attakbooler = false;
        AttakEnemybooler = false;
        navmeshCut.enabled = false;
        /*멈춰 있는 유닛은 모두 장애물로 판단, 장애물 확인하기 위해 Navmesh 업데이트 진행*/
        AstarPath.active.navmeshUpdates.ForceUpdate();
        AstarPath.active.FlushWorkItems();
        /*이동 코루틴을 진행하기 전 진행 중이던 모든 이동 관련 코루틴 종료*/
        StopCoroutine("CheckAttackMove");
        StopCoroutine("CheckMove");
        StopCoroutine("CheckAttackMoveEnemy");
        StartCoroutine("CheckMove");
    }
    IEnumerator CheckMove()
    {
        /*경로 계산이 끝날 때 까지 기다림(경로가 생성되면 reachedEndofPath(목적지 도달이 false가 됨)
         이를 위해 모든 이동 관련 작업은 ClearDestinationReached()로 마무리함)*/
        yield return new WaitUntil(() => !aiPath.reachedEndOfPath);

        if (!aiPath.reachedEndOfPath)
        {
            playerAnimator.SetBool("Move", true);

            /*목적지에 도착하지 않았거나, 멈춤상태가 아닌 동안 지속*/
            while (!aiPath.reachedEndOfPath && !aiPath.isStopped)
            {
                /*목적지에 도달한 경우*/
                if (aiPath.reachedEndOfPath || aiPath.rvoDensityBehavior.reachedDestination)
                {
                    /*애니메이션 조정, Move가 멈춘 후 Idle 애니메이션을 거쳐, Attack으로 가게 만들기 위해 업데이트 주기 2번을 기다림*/
                    playerAnimator.SetBool("Move", false);
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    /*움직임을 멈추고, ClearDestinationReached()와 SearchPath()로 경로를 지우고 경로를 찾아
                      aiPath.reachedEndOfPath를 true로 변경*/
                    aiPath.isStopped = true;
                    aiPath.rvoDensityBehavior.ClearDestinationReached();
                    aiPath.SearchPath();
                    aiPath.enabled = false;
                    Attakbooler = true;
                    /*멈춘 유닛을 장애물로 지정
                     (유닛으로 길을 막은 경우, 이를 인식하지 못하고 계속 목적지로 가려는 것을 막기 위함)*/
                    navmeshCut.enabled = true;
                    AstarPath.active.navmeshUpdates.ForceUpdate();
                    AstarPath.active.FlushWorkItems();
                    yield break;
                }
                yield return new WaitForFixedUpdate();
            }
            /*유닛이 멈춘 경우(목적지로 갈 경로가 없거나, 해당 위치에 유닛이 뭉쳐있을 경우)*/
            if (!aiPath.isStopped)
            {
                playerAnimator.SetBool("Move", false);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                aiPath.isStopped = true;
                aiPath.rvoDensityBehavior.ClearDestinationReached();
                aiPath.SearchPath();
                aiPath.enabled = false;
                Attakbooler = true;
                navmeshCut.enabled = true;
                AstarPath.active.navmeshUpdates.ForceUpdate();
                AstarPath.active.FlushWorkItems();
            }
        }
    }
    /*선택 + Attack + 위치*/
    [Command]
    public void MoveAttackTo(Vector3 targetPosition)
    {
        /* - 마지막 코루틴만 받아서 진행할 수 있게 통합 필요*/
        aiPath.enabled = true;
        aiPath.destination = targetPosition;
        aiPath.SearchPath();
        Attakbooler = false;
        AttakEnemybooler = false;
        navmeshCut.enabled = false;
        AstarPath.active.navmeshUpdates.ForceUpdate();
        AstarPath.active.FlushWorkItems();
        StopCoroutine("CheckAttackMove");
        StopCoroutine("CheckMove");
        StopCoroutine("CheckAttackMoveEnemy");
        StartCoroutine("CheckAttackMove");
    }
    IEnumerator CheckAttackMove()
    {
        /*경로 계산이 끝날 때 까지 기다림(경로가 생성되면 reachedEndofPath(목적지 도달이 false가 됨)
         이를 위해 모든 이동 관련 작업은 ClearDestinationReached()로 마무리함)*/
        yield return new WaitUntil(()=> !aiPath.reachedEndOfPath);
        if(!aiPath.reachedEndOfPath)
        {
            /*목적지에 도착할 때 까지*/
            while(!aiPath.reachedEndOfPath)
            {
                playerAnimator.SetBool("Move", true);

                /*이동 중 적이 없으면 계속 이동*/
                if(enemytarget == null)
                {
                    aiPath.isStopped = false;
                    Attakbooler = false;
                    playerAnimator.SetBool("Move", true);
                    yield return null;
                }

                /*이동 중 적이 있으면면 멈춘 후 공격 활성화*/
                if(enemytarget != null)
                {
                    playerAnimator.SetBool("Move", false);
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    aiPath.isStopped = true;
                    Attakbooler = true;
                    yield return null;
                }
                /*목적지에 도착한 경우 정지하고 while문 종료*/
                if(aiPath.reachedEndOfPath || aiPath.rvoDensityBehavior.reachedDestination)
                {
                    playerAnimator.SetBool("Move", false);
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    aiPath.isStopped = true;
                    aiPath.rvoDensityBehavior.ClearDestinationReached();
                    aiPath.SearchPath();
                    aiPath.enabled = false;
                    Attakbooler = true;
                    navmeshCut.enabled = true;
                    AstarPath.active.navmeshUpdates.ForceUpdate();
                    AstarPath.active.FlushWorkItems();
                    
                    yield break;
                }
                yield return new WaitForFixedUpdate();
            }
            /*테스트 후 추가*/
            if (!aiPath.isStopped)
            {
                playerAnimator.SetBool("Move", false);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                aiPath.isStopped = true;
                aiPath.rvoDensityBehavior.ClearDestinationReached();
                aiPath.SearchPath();
                aiPath.enabled = false;
                Attakbooler = true;
                navmeshCut.enabled = true;
                AstarPath.active.navmeshUpdates.ForceUpdate();
                AstarPath.active.FlushWorkItems();
            }
        }
    }
    /*선택 + A + 적 지정*/
    [Command]
    public void MoveAttackToEnemy(Transform Attackenemytarget)
    {
        /*적의 위치를 목적지로 지정, Attackenemytarget_Manager을 지정하여 가까운 적이 아닌 지정한 적을 타겟팅하게 함*/
        aiPath.enabled = true;
        aiPath.destination = Attackenemytarget.position;
        aiPath.SearchPath();
        Attackenemytarget_Manager = Attackenemytarget;
        Attakbooler = false;
        AttakEnemybooler = true;
        navmeshCut.enabled = false;
        AstarPath.active.navmeshUpdates.ForceUpdate();
        AstarPath.active.FlushWorkItems();
        StopCoroutine("CheckAttackMove");
        StopCoroutine("CheckMove");
        StopCoroutine("CheckAttackMoveEnemy");
        StartCoroutine("CheckAttackMoveEnemy");
    }
    IEnumerator CheckAttackMoveEnemy()
    {
        yield return new WaitUntil(()=> !aiPath.reachedEndOfPath);
        
        while(Attackenemytarget_Manager != null)
        {
            /*목적지를 이동하는 적으로 계속 변경*/
            playerAnimator.SetBool("Move", true);
            aiPath.destination = Attackenemytarget_Manager.position;
            /*지정 적이 공격 범위 안에 둘어오면 공격
            해당 코루틴은WaitForFixedUpdate()로 0.02초,
            UpdateTarget()는 0.04초 주기로 한 주기만큼은 이동, 공격을 반드시 진행하는데 공격에 더 비중을 주기 위해 if문을 적이 있는 경우로 설정*/
            if(enemytarget == Attackenemytarget_Manager)
            {
                aiPath.isStopped = true;
                Attakbooler = true;
                playerAnimator.SetBool("Move", false);
                yield return null;
            }
            /*지정 적이 공격 범위 안에 없으면 계속 이동*/
            else if(enemytarget != Attackenemytarget_Manager)
            {
                aiPath.isStopped = false;
                Attakbooler = false;
                playerAnimator.SetBool("Move", true);
                yield return null;
            }
            yield return new WaitForFixedUpdate();
        }
        /*while문을 벗어나면 유닛 정지*/
        aiPath.isStopped = true;
        aiPath.rvoDensityBehavior.ClearDestinationReached();
        aiPath.SearchPath();
        aiPath.enabled = false;
        Attakbooler = true;
        AttakEnemybooler = false;
        AstarPath.active.navmeshUpdates.ForceUpdate();
        AstarPath.active.FlushWorkItems();
        playerAnimator.SetBool("Move", false);
        
    }
    /*선택 + S or H*/
    [Command]
    public void StopTo()
    {
        /*유닛 정지 StopTo()는 AttFalse(), AttTrue()와 함께 사용*/
        StopCoroutine("CheckAttackMove");
        StopCoroutine("CheckMove");
        StopCoroutine("CheckAttackMoveEnemy");
        aiPath.isStopped = true;
        aiPath.rvoDensityBehavior.ClearDestinationReached();
        aiPath.SearchPath();
        aiPath.enabled = false;
        AttakEnemybooler = false;
        navmeshCut.enabled = true;
        AstarPath.active.navmeshUpdates.ForceUpdate();
        AstarPath.active.FlushWorkItems();
        playerAnimator.SetBool("Move", false);
    }
    /*유닛이 포탈 OnTriggerEnter할 경우 진행*/
    [Command]
    public void StopToPortal(Vector3 valid, float yRotation, Vector3 Startvalid)
    {
        /*정지를 수행*/
        StopCoroutine("CheckAttackMove");
        StopCoroutine("CheckMove");
        StopCoroutine("CheckAttackMoveEnemy");
        aiPath.isStopped = true;
        aiPath.rvoDensityBehavior.ClearDestinationReached();
        aiPath.SearchPath();
        aiPath.enabled = false;
        /*Teleport로 위치 이동*/
        aiPath.Teleport(valid);
        playerAnimator.SetBool("Move", false);
        AttakEnemybooler = false;
        navmeshCut.enabled = true;
        /*포탈 이동 후 앞을 볼 수 있도록 roataion 지정*/
        partToRotate.rotation = Quaternion.Euler(0f, yRotation, 0f);
        AstarPath.active.navmeshUpdates.ForceUpdate();
        AstarPath.active.FlushWorkItems();
        RpcTransLoad(valid, yRotation, Startvalid);
    }
    /*포탈 입장, 퇴장 위치에 이펙트 진행*/
    [ClientRpc]
    void RpcTransLoad(Vector3 valid, float yRotation, Vector3 Startvalid)
    {
        GameObject m_makedObjectin = Instantiate(PortalEf_, Startvalid, PortalEf_.transform.rotation).gameObject;
        GameObject m_makedObjectout = Instantiate(PortalEf_, valid, PortalEf_.transform.rotation).gameObject;
        Destroy(m_makedObjectin, 5f);
        Destroy(m_makedObjectout, 5f);

        /*포탈 이동 시 해당 위치에 이동 가능한 위치를 확인하고 이동하는데
         이 때 포탈로 입장 후 바로 퇴장하는 현상이 발생, 이를 막기 위해 진행*/
        StartCoroutine(Collider_T(valid, yRotation));
        
        InGameSoundManager.Instance?.UnitPortalSound(Startvalid);
        InGameSoundManager.Instance?.UnitPortalSound(valid);
    }
    
    private IEnumerator Collider_T(Vector3 valid, float yRotation)
    {
        float startTime = Time.time;
        BoxCollider boxCollider = this.GetComponent<BoxCollider>();
        int framesConditionMet = 0;
        const int requiredFrames = 3;
        /*서버에서 position을 valid로 이동시키면, 클라이언트는 위치 보간에 의해 날라가는 것이 보임
         추가적으로 이 보간으로 인해 이동 경로에 포탈이 있으면 다시 해당 포탈에 OnTriggerEnter가 진행 됨
         이를 막기 위해 OnTriggerEnter 시 BoxCollider를 비활성화하고 scale을 (0.1,0.1,0.1)으로 조정해둠*/
        while (Time.time - startTime < 0.3f)
        {
            /*3프레임 동안 목적지와 거리를 측정하여 포탈 이동이 완료되면 다시 scale을 조정하고 collider 활성화*/
            if (Vector3.Distance(partToRotate.position, valid) <= 0.5f)
            {
                framesConditionMet++;
                if (framesConditionMet >= requiredFrames && boxCollider != null)
                {
                    partToRotate.rotation = Quaternion.Euler(0f, yRotation, 0f);
                    partToRotate.localScale = new Vector3(3.2f, 3.2f, 3.2f);
                    boxCollider.enabled = true;
                    break;
                }
            }
            else
            {
                framesConditionMet = 0;
            }
            yield return new WaitForSeconds(0.03f);
        }
        /*0.3초 후에도 포탈 이동을 못했으면 오브젝트 scale을 조정하고 collider을 활성화하여 다시 이용 가능하게 만듬*/
        if (framesConditionMet < requiredFrames && boxCollider != null)
        {
            partToRotate.rotation = Quaternion.Euler(0f, yRotation, 0f);
            partToRotate.localScale = new Vector3(3.2f, 3.2f, 3.2f);
            boxCollider.enabled = true;
        }
    }
    /*유닛 일시 정지*/
    [Command]
    public void StopDone()
    {
        aiPath.isStopped = false;
    }
    /*유닛 공격 비활성화*/
    [Command]
    public void AttFalse()
    {
        Attakbooler = false;
    }
    /*유닛 공격 활성화*/
    [Command]
    public void AttTrue()
    {
        Attakbooler = true;
    }
    /*유닛 경험치, 레벨 조정*/
    public void GetExp(float Exp)
    {
        if (UnitLevel >= 16)
        {
            UnitCurrentExp = UnitMaxExp;
        }
        else if(UnitLevel < 16)
        {
            UnitCurrentExp += Exp;
            /*협동 보스, 라인 보스 등을 통해 많은 경험치 획득 시 레벨업 대응하기 위해 while문 사용*/
            while (UnitCurrentExp >= UnitMaxExp)
            {
                UnitLevel++;
                LevelUpEf();
                UnitCurrentExp -= UnitMaxExp;
                UnitMaxExp = UnitLevel * 50f;
                if (UnitLevel >= 16)
                {
                    UnitCurrentExp = UnitMaxExp;
                    break;
                }
            }
        }
    }
    /*레벨업 이펙트 진행*/
    [ClientRpc]
    void LevelUpEf()
    {
        Vector3 hitCenter = gameObject.transform.position;
        GameObject m_makedObject = Instantiate(LevelUpEf_, hitCenter, LevelUpEf_.transform.rotation).gameObject;
        m_makedObject.transform.SetParent(gameObject.transform);

        Destroy(m_makedObject, 5f);

        InGameSoundManager.Instance?.UnitLevelUpSound(transform.position);
    }
    /*유닛 생성 이펙트*/
    [ClientRpc]
    public void SpwanEffect()
    {
        Vector3 hitCenter = gameObject.transform.position;
        GameObject m_makedObject = Instantiate(SpawnEf_, hitCenter, SpawnEf_.transform.rotation).gameObject;

        Destroy(m_makedObject, 5f);
    }
    /*공격력 버프 적용*/
    public void ApplyBuff_P(string type, float duration, float percent)
    {
        if(upgrade_fam)
            StartCoroutine(Buff_P(type, duration, percent));
    }

    public IEnumerator Buff_P(string type, float duration, float percent)
    {
        char lastChar = gameObject.tag[gameObject.tag.Length-1];
        /*공격력 버프는 (기본공격력 + 종족공격력업그레이드 + (기본공격력 * 유닛강화 공격력 / 100) * percent (퍼센트는 버프 유닛의 공격력 영향)*/
        float R_Dam = (gameObject.GetComponent<Dam_Fam>().attackPower + (int)(upgrade_fam.upgradeValues[(int)lastChar-49, (int)gameObject.GetComponent<Dam_Fam>().charatcterRace]) + (gameObject.GetComponent<Dam_Fam>().attackPower * upgradeA_Num_[UnitUpgradeValue]/100));        
        
        float InPower = R_Dam * percent;

        /*버프는 중복 적용 가능*/
        Buff_Power += InPower;
        /*적용 버프를 이미지로 보여주기 위해서 동일한 버프 개수 확인*/
        if (Buff_Count.ContainsKey(type))
        {
            Buff_Count[type]++;
        }
        else
        {
            Buff_Count[type] = 1;
        }

        /*동일한 버프라도 계수가 다를 수 있기 때문에 지속시간 지난 후 해당 버프를 제거*/
        yield return new WaitForSeconds(duration);

        Buff_Power -= InPower; 
        if (Buff_Count.ContainsKey(type))
        {
            Buff_Count[type]--;
        }
        else
        {
            Buff_Count[type] = 0;
        }
    }
    /*공격속도 버프 적용*/
    public void ApplyBuff_S(string type, float duration, float percent)
    {
        if(upgrade_fam)
            StartCoroutine(Buff_S(type, duration, percent));
    }

    public IEnumerator Buff_S(string type, float duration, float percent)
    {
        char lastChar = gameObject.tag[gameObject.tag.Length-1];
        /*공격속도 버프는 (기본공격속도 + (종족공격속도업그레이드/100) + (기본공격속도 * 유닛강화 공격속도 / 100) * percent (퍼센트는 버프 유닛의 공격력 영향)*/
        float R_Speed = (gameObject.GetComponent<Dam_Fam>().attack_Speed + (upgrade_fam.upgradeValues[(int)lastChar-49, (int)gameObject.GetComponent<Dam_Fam>().charatcterRace + 2]/100) + (gameObject.GetComponent<Dam_Fam>().attack_Speed * upgradeS_Num_[UnitUpgradeValue]/100));   

        float InSpeed = R_Speed * percent;
        /*버프는 중복 적용 가능*/
        Buff_Speed += InSpeed;
        /*적용 버프를 이미지로 보여주기 위해서 동일한 버프 개수 확인*/
        if (Buff_Count.ContainsKey(type))
        {
            Buff_Count[type]++;
        }
        else
        {
            Buff_Count[type] = 1;
        }
        
        /*동일한 버프라도 계수가 다를 수 있기 때문에 지속시간 지난 후 해당 버프를 제거*/
        yield return new WaitForSeconds(duration);

        Buff_Speed -= InSpeed;
        if (Buff_Count.ContainsKey(type))
        {
            Buff_Count[type]--;
        }
        else
        {
            Buff_Count[type] = 0;
        }
    }

    /*공격 애니메이션에 공격속도 반영*/
    private float CalculateCurrentSpeed() 
    {
        /*반영 공격속도 : 기본공격속도 + (종족공격속도업그레이드/100) + (기본공격속도 * 유닛강화 공격속도 / 100) + 버프 공격속도*/
        char lastChar = gameObject.tag[gameObject.tag.Length - 1];
        float attackSpeed = gameObject.GetComponent<Dam_Fam>().attack_Speed;
        float upgradeSpeed = (upgrade_fam.upgradeValues[(int)lastChar - 49, (int)gameObject.GetComponent<Dam_Fam>().charatcterRace + 2]/100);
        float upgradeModifier = attackSpeed * upgradeS_Num_[UnitUpgradeValue] / 100f;

        return attackSpeed + upgradeSpeed + upgradeModifier + Buff_Speed;
    }
}
