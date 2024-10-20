using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public interface IEnemy 
{
    float enemySpeed { get; set; }
    Transform target { get; set; }
    Transform enemyCanvas { get; set; }
    int wavepointIndex { get; set; }
    Vector3 rot { get; set; }
    float R_stamina { get; set; }
    Transform Ecenter { get; set; }

    void ApplySpeedDown(float time, float percent);
}
[System.Serializable]
public class EnemyDropItem
{
    public string itemName;
    public float dropChance;
}

/*Enemy, Boss, CenterBoss 모두 IEnemy 인터페이스를 상속*/
public class Enemy : NetworkBehaviour, IEnemy
{
    public float enemySpeed { get; set; } = 7f;
    public Transform target { get; set; }
    public Transform enemyCanvas { get; set; }
    public int wavepointIndex { get; set; } = 0;
    public Vector3 rot { get; set; } = new Vector3(0, 90, 0);
    
    public Transform Ecenter_;
    public Transform Ecenter
    {
        get { return Ecenter_; }
        set { Ecenter_ = value; }
    }

    private List<Material> clonedMaterials = new List<Material>();
    public Slider staminaSlider;
    public Slider D_staminaSlider;

    private bool isRotating = false;
    public List<EnemyDropItem> DropItems;

    [SerializeField]
    [SyncVar(hook = nameof(OnStaminaChanged))]
    public float stamina = 100f;
    public float R_stamina
    {
        get { return stamina; }
        set { stamina = value; }
    }
    private bool isInitialized = false;
    private Transform[] selectedWaypoints;
    private GameData gameDataReference;
    public int playerIndex;
    public List<GameObject> embeddedWeapons = new List<GameObject>();
    private bool isDying = false;

    private bool isSpeedReduced = false;
    private float currentSpeedReductionPercent = 0f;
    private Coroutine speedDownCoroutine;
    public float EnemyExp = 2;

    public Animator MyAnim;
    public Vector3 TScale;

    public GameObject GoldEffect;
    public GameObject DamEffect;

    void Start()
    {
        if(isClient)
        {
            /*생성 시 사운드 재생과 포탈에서 나오는 모습을 자연스럽게 표현하기 위해 클라이언트에서 scale 증가 코루틴을 진행*/
            InGameSoundManager.Instance?.EnemySpawnSound(transform.position);
            StartCoroutine(IncreaseScaleOverTime());

            /*적이 죽을 때 Material에 변화를 주어 사라지는 모습을 나타냄 이를 위해 Clone머테리얼 생성*/
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material clonedMaterial = new Material(materials[i]);
                    materials[i] = clonedMaterial;
                    clonedMaterials.Add(clonedMaterial);
                }
                renderer.materials = materials;
            }
        }
        /*체력바를 조정, 체력바는 실제 체력바와 시각적 효과를 위해 천천히 떨어지는 체력바를 같이 사용*/
        if (staminaSlider)
        {
            staminaSlider.maxValue = R_stamina;
            staminaSlider.value = R_stamina;
        }
        if (D_staminaSlider)
        {
            D_staminaSlider.maxValue = R_stamina;
            D_staminaSlider.value = R_stamina;
        }
    }
    /*유저의 인덱스에 따라 스폰 위치 조정*/
    public void EnemyInitialize(int playerIndex, GameData gameData)
    {
        switch (playerIndex)
        {
            case 0:
                InitializeWaypoints(WaypointsManger.points1);
                break;
            case 1:
                InitializeWaypoints(WaypointsManger.points2);
                break;
            case 2:
                InitializeWaypoints(WaypointsManger.points3);
                break;
            case 3:
                InitializeWaypoints(WaypointsManger.points4);
                break;
            default:
                Debug.LogError("Invalid player index");
                return;
        }
        this.playerIndex = playerIndex;
        this.gameDataReference = gameData;
        target = selectedWaypoints[0];
        isInitialized = true;
    }
    public void InitializeWaypoints(Transform[] waypoints)
    {
        selectedWaypoints = waypoints;
    }
    /*적 Scale 증가*/
    IEnumerator IncreaseScaleOverTime()
    {
        Vector3 initialScale = Vector3.zero;
        Vector3 targetScale = TScale;

        float elapsed = 0;

        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.5f;

            gameObject.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);

            yield return null;
        }

        gameObject.transform.localScale = targetScale;
    }

    void Update ()
    {
        if (!isInitialized)
        {
            return;
        }
        Vector3 dir = target.position - transform.position;
        transform.Translate(dir.normalized * enemySpeed * Time.deltaTime, Space.World);
        /*WayPoint와의 거리가 0.2f 보다 작으면 다음 Waypoint를 타겟으로 지정*/
        if(Vector3.Distance(transform.position, target.position) <= 0.2f)
        {
            GetNextWaypoint();
        }
        /*너무 가까운 위치에서 회전 시 부자연스럽기 때문에 1.8f에서 회전 시작*/
        if(Vector3.Distance(transform.position, target.position) <= 1.8f && wavepointIndex < selectedWaypoints.Length && !isRotating)
        {
            StartCoroutine(RotateOverTime(rot, 0.5f));
        }
    }
    void GetNextWaypoint()
    {
        /*일반 적은 계속해서 waypoint를 돌기 때문에 마지막 위치에서 다시 0으로 리셋*/
        if(wavepointIndex >= selectedWaypoints.Length - 1)
        {
            wavepointIndex = 0;
        }
        /*그외는 waypoint++*/
        else
        {
            wavepointIndex++;   
        }
        target = selectedWaypoints[wavepointIndex];
        /*각 waypoint에서 다음 waypoint는 무조건 직각이므로 y축 기준으로 90도를 계속 빼면 적 유닛의 회전값 조정 가능*/
        rot = rot - new Vector3(0,90,0);
    }
    IEnumerator RotateOverTime(Vector3 eulerAngle, float duration)
    {
        /*코루틴 진행 중 또 회전하는 것을 막기 위해 사용*/
        isRotating = true;
        Quaternion initialRotation = transform.rotation;
        Quaternion finalRotation = Quaternion.Euler(eulerAngle);
        float elapsed = 0f;

        /*시간에 걸쳐 Lerp하게 회전값 조정*/
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            transform.rotation = Quaternion.Lerp(initialRotation, finalRotation, normalizedTime);
            yield return null;
        }

        transform.rotation = finalRotation;
        isRotating = false;
    }

    IEnumerator HandleDeath()
    {
        if(isOwned)
        {
            /*적 사망시 서버측에서 먼저 진행함*/
            DestroyMon();
            /*골드 획득 이펙트 진행, 적 중아 위치에서 0.5f 위에서 진행*/
            Vector3 goldEffectPosition = Ecenter_.position + new Vector3(0, 0.5f, 0);
            Quaternion goldEffectRotation = Quaternion.Euler(0, 0, 0);
            GameObject Gold_Object = Instantiate(GoldEffect, goldEffectPosition, goldEffectRotation);
            /*골드 획득에 따라 다른 텍스트 출력*/
            Gold_Object.GetComponent<Gold_ro>().PrintGold(1);
            Destroy(Gold_Object, 1.5f);
            InGameSoundManager.Instance?.MonsterDeathSound(transform.position);
        }
        yield return new WaitForSeconds(0.25f);
        float elapsedTime = 0f;
        float duration = 0.5f;
        Vector3 initialScale = gameObject.transform.localScale;
        Vector3 targetScale = Vector3.zero;

        while (elapsedTime < duration)
        {
            /*캐릭터가 무기를 적에게 부착할 수 있기 때문에
             scale을 줄이면 무기만 공중에 남아있는 상황을 방지하기 무기를 우선 삭제*/
            foreach (var weapon in embeddedWeapons)
            {
                if (weapon != null)
                    weapon.SetActive(false);
            }
            /*적의 머테리얼을 조정하여 사라지는 효과 적용*/
            float dissolveAmount = Mathf.Lerp(0, 1, elapsedTime / duration);
            foreach (Material mat in clonedMaterials)
            {
                mat.SetFloat("_Cutout", dissolveAmount);
            }
            /*여기에 scale을 같이 줄여 사라지는 효과 보강*/
            gameObject.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        foreach (Material mat in clonedMaterials)
        {
            mat.SetFloat("_Cutout", 1);
        }
        gameObject.transform.localScale = targetScale;
        if(isOwned)
            DestroyMonEnd();
    }
    [Command]
    private void DestroyMon()
    {
        /*적의 움직임을 멈추고 죽는 애니메이션 진행*/
        enemySpeed = 0;
        MyAnim.SetBool("isDie", true);
        /*tag를 변경해 유닛이 해당 적을 공격하지 못하도록 변경*/
        this.gameObject.tag = "Enemy_Dead";
        /*적의 숫자, 골드는 게임에 영향이 크기때문에 우선 진행*/
        gameDataReference.ECountDo(playerIndex);
        gameDataReference.AddGold(1);
        /*전역으로 (경험치 / 보유유닛) 으로 경험치 부여*/
        string unitTag = "Unit_p" + (playerIndex + 1);
        GameObject[] unitsWithTag = GameObject.FindGameObjectsWithTag(unitTag);
        float experienceToGive = EnemyExp/unitsWithTag.Length;

        foreach (GameObject unit in unitsWithTag)
        {
            UnitManager_Net unitExperience = unit.GetComponent<UnitManager_Net>();
            if (unitExperience != null)
            {
                unitExperience.GetExp(experienceToGive);
            }
        }
        /*아이템을 확률적으로 드랍*/
        foreach(var lootItem in DropItems)
        {
            if (Random.Range(0f, 30000f) <= lootItem.dropChance)
            {
                gameDataReference.AddLoot(lootItem.itemName);
            }
        }
    }
    [Command]
    private void DestroyMonEnd()
    {
        /*적 사망을 클라이언트에서 표현 후 제거*/
        NetworkServer.UnSpawn(gameObject);
        NetworkServer.Destroy(gameObject);
    }

    public void OnStaminaChanged(float oldStamina, float newStamina)
    {
        UpdateStaminaUI(newStamina);
        int ShowDam = (int)(oldStamina - newStamina);
        if(ShowDam > 0 )
        {
            /*데미지 폰트를 3D로 출력*/
            Vector3 DamEffectPosition = Ecenter_.position + new Vector3(0, 0.5f, 0.5f);
            Quaternion DamEffectRotation = Quaternion.Euler(0, 0, 0);
            GameObject Dam_Object = Instantiate(DamEffect, DamEffectPosition, DamEffectRotation);
            /*DamageText에서 데미지 수치에 따라 색상, 크기, 속도 조정, 탄젠트 함수로 랜덤하게 공격 폰트가 올라가는 효과*/
            Dam_Object.GetComponent<DamgeText>().ShowDamage(ShowDam);
            Destroy(Dam_Object, 1.2f);
        }
    }

    private void UpdateStaminaUI(float newStamina)
    {
        if (staminaSlider != null)
        {
            /*staminaSlider의 value가 변하면 D_staminaSlider는 Lerp하게 value로 천천히 줄어듬*/
            staminaSlider.value = newStamina;
        }
        if (stamina <= 0 && !isDying)
        {
            staminaSlider.gameObject.SetActive(false);
            D_staminaSlider.gameObject.SetActive(false);
            isDying = true;
            StartCoroutine(HandleDeath());
        }
    }

    /*버프 유닛의 이동속도 감소 디버프 적용*/
    public void ApplySpeedDown(float duration, float percent)
    {
        /*이미 디버프가 적용 중일 경우, 디버프 수치가 더 높거나 같을 때 해당 값으로 다시 디버프 시작
          디버프는 중복 불가*/
        if (isSpeedReduced && percent >= currentSpeedReductionPercent)
        {
            if (speedDownCoroutine != null)
            {
                StopCoroutine(speedDownCoroutine);
                currentSpeedReductionPercent = 0f;
                isSpeedReduced = false;
                enemySpeed = 7f;
            }
            speedDownCoroutine = StartCoroutine(SpeedDown(duration, percent));
        }
        
        else if (!isSpeedReduced)
        {
            speedDownCoroutine = StartCoroutine(SpeedDown(duration, percent));
        }
    }

    public IEnumerator SpeedDown(float duration, float percent)
    {
        currentSpeedReductionPercent = percent;
        isSpeedReduced = true;
        float originalSpeed = enemySpeed;
        /*디버프가 10%면 기본속도의 90%로 수정, 20%면 기본속도의 80%로 수정*/
        float reducedSpeed = Mathf.Max(0, originalSpeed * (1f - percent));

        enemySpeed = reducedSpeed;

        yield return new WaitForSeconds(duration);

        currentSpeedReductionPercent = 0f;
        isSpeedReduced = false;
        enemySpeed = originalSpeed;
    }
    
}
