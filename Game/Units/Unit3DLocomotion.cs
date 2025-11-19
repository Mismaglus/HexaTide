using UnityEngine;

// 只负责【移动】相关的动画参数驱动：Speed/MoveSpeed/IsGrounded/(可选)IsWalking
// 技能由独立的 AbilityRunner 处理，本组件不涉及任何技能参数。
[DisallowMultipleComponent]
public class Unit3DLocomotion : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;                  // 自动从子物体绑定

    [Tooltip("Animator 中用于驱动 Idle↔Locomotion 的速度参数名（常用 'Speed'）")]
    [SerializeField] private string speedParam = "Speed";

    [Tooltip("兼容 Synty/其它控制器时，可同时写入的速度参数名（常用 'MoveSpeed'）。留空则不写。")]
    [SerializeField] private string moveSpeedParam = "MoveSpeed";

    [Tooltip("若 Animator 里有 IsWalking，并且你希望用布尔做条件，可勾选并写入（值=速度>阈值）")]
    [SerializeField] private bool writeIsWalking = true;

    [SerializeField] private string isWalkingParam = "IsWalking";

    [Header("Speed & Timing")]
    [SerializeField] private float speedScale = 1f;              // 乘法系数，方便整体校准
    [SerializeField] private float minMoveSpeed = 0.05f;         // 低于该值视为 Idle
    [SerializeField] private Game.Units.Unit unit;               // 自动从父物体绑定，用于 secondsPerTile
    [SerializeField] private float refSecondsPerTile = 1f;       // 参考：每格 1 秒（用于调 animator.speed）

    [Header("Grounding")]
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [Tooltip("战棋通常无需落地检测，勾选后每帧把 IsGrounded 写为 true。")]
    [SerializeField] private bool alwaysGrounded = true;

    [Header("Teleport Handling")]
    [Tooltip("单帧位移超过该距离时视为瞬移（忽略该帧速度），避免动画误触发奔跑")]
    [SerializeField] private float snapTeleportDistance = 0.5f;

    [Header("Rotation")]
    [SerializeField] private float rotateLerp = 12f;             // 转向平滑
    [SerializeField] private bool rotateOnlyWhenMoving = true;   // 仅在移动时转向

    // 运行时缓存
    private Transform _parent;
    private Vector3 _lastParentPos;
    private bool _hasLastParentPos;

    private float _speed;                 // 计算得到的平面速度（单位/秒）

    // Animator 参数哈希
    private int _speedHash;
    private int _moveSpeedHash;
    private int _isGroundedHash;
    private int _isWalkingHash;

    // 当前 Controller 是否真的包含这些参数（避免 Set* 不存在的参数产生警告）
    private bool _hasSpeed, _hasMoveSpeed, _hasIsGrounded, _hasIsWalking;
    private bool _parametersCached;

    // ---------- 自动绑定 ----------
    private void TryAutoBind()
    {
        if (unit == null) unit = GetComponentInParent<Game.Units.Unit>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        // 速度采样：优先用 Unit 组件所在的 Transform
        _parent = (unit != null) ? unit.transform : (transform.parent != null ? transform.parent : transform);
        _hasLastParentPos = false;
        _parametersCached = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoBind();
        CacheParamHashes();
        CacheAnimatorParams();
    }
#endif

    private void Awake()
    {
        TryAutoBind();
        CacheParamHashes();
        CacheAnimatorParams();

        _lastParentPos = (_parent != null) ? _parent.position : transform.position;
        _hasLastParentPos = true;
    }

    private void CacheParamHashes()
    {
        _speedHash = Animator.StringToHash(speedParam);
        _moveSpeedHash = Animator.StringToHash(moveSpeedParam);
        _isGroundedHash = Animator.StringToHash(isGroundedParam);
        _isWalkingHash = Animator.StringToHash(isWalkingParam);
    }

    private void CacheAnimatorParams()
    {
        _hasSpeed = _hasMoveSpeed = _hasIsGrounded = _hasIsWalking = false;
        _parametersCached = false;
        if (animator == null) return;
        if (animator.runtimeAnimatorController == null) return;
        var set = new System.Collections.Generic.HashSet<int>();
        foreach (var p in animator.parameters) set.Add(p.nameHash);
        _hasSpeed = set.Contains(_speedHash);
        if (!string.IsNullOrEmpty(moveSpeedParam)) _hasMoveSpeed = set.Contains(_moveSpeedHash);
        _hasIsGrounded = set.Contains(_isGroundedHash);
        _hasIsWalking = writeIsWalking && set.Contains(_isWalkingHash);
        _parametersCached = true;
    }

    private void Update()
    {
        // 若层级变动，尝试重新绑定
        if (_parent == null || (unit == null && transform.parent != _parent))
            TryAutoBind();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            if (_parametersCached)
            {
                _parametersCached = false;
                _hasSpeed = _hasMoveSpeed = _hasIsGrounded = _hasIsWalking = false;
            }
        }
        else if (!_parametersCached)
        {
            CacheAnimatorParams();
        }

        // 计算平面速度（基于父物体或自身）
        Vector3 parentPos = (_parent != null) ? _parent.position : transform.position;
        if (!_hasLastParentPos)
        {
            _lastParentPos = parentPos;
            _hasLastParentPos = true;
        }

        Vector3 delta = parentPos - _lastParentPos;
        float snapDist = Mathf.Max(0f, snapTeleportDistance);
        if (snapDist > 0f && delta.sqrMagnitude > snapDist * snapDist)
        {
            // 视为瞬移，忽略该帧速度
            delta = Vector3.zero;
        }
        _lastParentPos = parentPos;

        Vector3 planar = new Vector3(delta.x, 0f, delta.z);
        float rawSpeed = planar.magnitude / Mathf.Max(Time.deltaTime, 1e-5f);
        _speed = rawSpeed * speedScale;

        // 调整 Animator 播放速率以匹配每格用时（可选）
        if (animator != null)
        {
            float secondsPerTile = (unit != null && unit.secondsPerTile > 0.01f) ? unit.secondsPerTile : refSecondsPerTile;
            float animRate = refSecondsPerTile / Mathf.Max(0.01f, secondsPerTile);
            animator.speed = animRate;
        }

        // 写入 Animator 参数（仅在该参数实际存在时写入）
        if (animator != null)
        {
            if (_hasSpeed) animator.SetFloat(_speedHash, _speed);
            if (_hasMoveSpeed) animator.SetFloat(_moveSpeedHash, _speed);
            if (_hasIsGrounded && alwaysGrounded) animator.SetBool(_isGroundedHash, true);
            if (_hasIsWalking) animator.SetBool(_isWalkingHash, _speed > minMoveSpeed);
        }

        // 朝向：朝当前位移方向插值转向
        if (!rotateOnlyWhenMoving || _speed > minMoveSpeed)
        {
            if (planar.sqrMagnitude > 1e-6f)
            {
                Quaternion target = Quaternion.LookRotation(planar.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target,
                    1f - Mathf.Exp(-rotateLerp * Time.deltaTime)
                );
            }
        }
    }

    // 外部强制朝向（可选）
    public void FaceWorldDirection(Vector3 worldDir)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 1e-6f) return;
        Quaternion target = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
        transform.rotation = target;
    }
}
