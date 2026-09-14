namespace Demo.Combat
{
    /// <summary>
    /// 连招链纯逻辑，不依赖 UnityEngine，可直接单元测试。
    ///
    /// 规则：
    ///  - 每按一次攻击推进一段，第一次从第 1 段开始
    ///  - 攻击进行中的按键只入队一个，当前段播完立刻接下一段
    ///  - 一段播完后超过 window 秒没再按，链自动重置回第 1 段
    ///  - 翻滚 / 跳跃 / 受击 / 倒地调用 Break() 手动打断
    ///  - 打完最后一段后，下一次按键回到第 1 段
    ///
    /// 注意"连招窗口"是从上一段播完开始算的，不是从按键开始算。
    /// 因为动画长度不受按键控制，从按键算窗口会让长动画永远接不上。
    /// </summary>
    public class ComboChain
    {
        private readonly int _length;
        private readonly float _window;
        private readonly float _finalMultiplier;

        private int _index = -1;
        private bool _playing;
        private bool _queued;
        private float _idleTimer;

        public ComboChain(int length, float window, float finalMultiplier)
        {
            _length = length < 1 ? 1 : length;
            _window = window;
            _finalMultiplier = finalMultiplier;
        }

        /// <summary>当前正在播 / 刚播完的段序号。0 起算，-1 表示不在连招中。</summary>
        public int CurrentIndex { get { return _index; } }

        /// <summary>是否有一段正在播放。</summary>
        public bool IsPlaying { get { return _playing; } }

        /// <summary>是否已经缓存了下一次按键。</summary>
        public bool HasQueuedInput { get { return _queued; } }

        /// <summary>当前段的伤害倍率。最后一段翻倍，其余为 1。</summary>
        public float DamageMultiplier
        {
            get { return _index == _length - 1 ? _finalMultiplier : 1f; }
        }

        /// <summary>
        /// 按下攻击键。
        /// 返回本次应当立刻播放的段序号；返回 -1 表示只是入队，等 Release() 再取。
        /// </summary>
        public int Press()
        {
            if (_playing)
            {
                _queued = true;
                return -1;
            }

            int next = (_index >= 0 && _idleTimer <= _window) ? _index + 1 : 0;

            if (next >= _length)
            {
                next = 0;
            }

            Start(next);
            return next;
        }

        /// <summary>
        /// 当前段动画播完时调用。
        /// 返回下一段应当播放的段序号；返回 -1 表示连招到此为止。
        /// </summary>
        public int Release()
        {
            _playing = false;
            _idleTimer = 0f;

            bool queued = _queued;
            _queued = false;

            if (!queued || _index < 0)
            {
                return -1;
            }

            int next = _index + 1;

            if (next >= _length)
            {
                next = 0;
            }

            Start(next);
            return next;
        }

        /// <summary>连招窗口计时。窗口内没等到下一次按键就重置。</summary>
        public void Tick(float deltaTime)
        {
            if (_playing || _index < 0)
            {
                return;
            }

            _idleTimer += deltaTime;

            if (_idleTimer > _window)
            {
                Reset();
            }
        }

        /// <summary>手动打断。翻滚 / 跳跃 / 受击 / 倒地时调用。</summary>
        public void Break()
        {
            Reset();
        }

        private void Start(int index)
        {
            _index = index;
            _playing = true;
            _queued = false;
            _idleTimer = 0f;
        }

        private void Reset()
        {
            _index = -1;
            _playing = false;
            _queued = false;
            _idleTimer = 0f;
        }
    }
}
