/// <summary>
/// 이브이 영웅증강 v2 "나인이볼부스트"의 소환 순서와 종별 버프 수치.
///
/// 기획 확정(2026-08-19): 영웅 이브이가 <b>스킬을 1회 시전할 때마다 진화체 1종을 순서대로</b>
/// 봇으로 소환하고, 그 종에 대응하는 버프를 <b>영웅 이브이 자신에게</b> 건다.
/// 버프를 받는 것은 소환된 봇이 아니라 언제나 "가장 강한 이브이"다 — 예를 들어 샤미드가 나오면
/// 샤미드가 아니라 이브이가 보호막을 얻는다.
///
/// 봇은 돌연변이 시너지 봇과 완전히 같은 형식이다(<c>BattleManager.CreateBotUnit</c>) —
/// source=null인 전투 전용 유닛이라 전투가 끝나면 <c>Cleanup()</c>에서 통째로 사라지고
/// 보드/시너지/복원에는 흔적을 남기지 않는다.
///
/// <b>수치는 코드 상수로 관리한다</b> — <see cref="SynergyConstants"/>와 같은 방침이다
/// (기획 결정: 밸런스 조정 = 이 표만 수정). 아래 값은 전부 기존 시너지 티어를 기준으로 잡은
/// 초안이며, 엑셀 밸런스 확정 시 <see cref="Entries"/>의 value만 갈아끼우면 된다.
///
/// <b>전부 비율(%)로 잡은 이유</b> — 이 버프는 "가장 강한 이브이" 한 마리에만 쌓인다.
/// 절대 가산으로 두면 3성·아이템을 몰아준 이브이일수록 체감이 옅어져, 한 마리에 투자를 몰아주는
/// 증강 설계와 정확히 반대로 움직인다. 비율이면 투자한 만큼 같이 커진다.
/// (보호막만 예외적으로 spellPower 비례 — WATER 시너지와 같은 공식이다.)
/// </summary>
public static class HeroEeveeBoostTable
{
    /// <summary>버프가 건드리는 스탯. 값의 의미는 항목별로 다르다(<see cref="Entry.value"/> 참고).</summary>
    public enum Stat
    {
        Shield,       // spellPower × value 만큼 보호막 가산(WATER 시너지와 동일 공식)
        AttackSpeed,  // attackSpeed × (1 + value)
        Defense,      // defense    × (1 + value)
        Attack,       // attack     × (1 + value)
        CritChance,   // critChance + value (절대 가산, 1.0 상한)
        MaxHp,        // maxHp      × (1 + value), 늘어난 만큼 currentHp도 회복
        ManaRegen,    // manaGainMultiplier + value (가산)
        SpellPower    // spellPower × (1 + value)
    }

    /// <summary>소환 1회분 — 어떤 종이 나오고 어떤 버프가 붙는지.</summary>
    public readonly struct Entry
    {
        /// <summary>소환할 진화체의 영문명(PokemonData.pokemonNameEn).</summary>
        public readonly string speciesNameEn;

        /// <summary>영웅 이브이에게 걸 버프 종류.</summary>
        public readonly Stat stat;

        /// <summary>버프 수치. 비율형은 0.20 = +20%, CritChance는 0.10 = +10%p.</summary>
        public readonly float value;

        /// <summary>로그·QA 표시용 한글 라벨.</summary>
        public readonly string label;

        public Entry(string speciesNameEn, Stat stat, float value, string label)
        {
            this.speciesNameEn = speciesNameEn;
            this.stat  = stat;
            this.value = value;
            this.label = label;
        }
    }

    /// <summary>
    /// 소환 순서(기획 확정 2026-08-19). <b>배열 순서가 곧 등장 순서다</b> — 순서를 바꾸면
    /// 게임 내 등장 순서가 그대로 바뀐다.
    ///
    /// 각 수치의 근거(<see cref="SynergyConstants"/> 대비):
    ///   샤미드   보호막 0.40 — WATER t2(0.35)~t3(0.50) 사이. 1회성으로 소모되는 값이라 조금 세게.
    ///   쥬피썬더 공속 +15% — FLYING t1(10%)~t2(20%) 사이.
    ///   부스터   방어 +25% — FIRE t2(25%)와 동일.
    ///   에브이   공격 +20% — BREAKER t2(20%)와 동일.
    ///   블래키   치명 +10%p — NORMAL t2(10%)와 동일.
    ///   리피아   체력 +20% — GROUND t1(15%)~t2(30%) 사이.
    ///   글레이시아 마나 +30% — ETHEREAL t1(20%)~t2(40%) 사이, 치어리더 마나 선택지(30%)와 동일.
    ///   님피아   주문력 +30% — DRAGON t1(20%)~t2(40%) 사이.
    ///
    /// 전반적으로 "시너지 2티어 1개"에 해당하는 크기로 맞췄다. 8개가 전부 쌓이면 세지만,
    /// 스킬 8회를 시전할 만큼 전투가 길어야 하고 그 8개가 한 마리에만 붙으므로 이 정도가 균형점이다.
    /// 뒤로 갈수록 실제로 발동할 확률이 낮아지는 것을 감안해 후반 항목을 조금 크게 잡았다.
    /// </summary>
    public static readonly Entry[] Entries =
    {
        new("Vaporeon", Stat.Shield,      0.40f, "보호막"),
        new("Jolteon",  Stat.AttackSpeed, 0.15f, "공격속도"),
        new("Flareon",  Stat.Defense,     0.25f, "내구도"),
        new("Espeon",   Stat.Attack,      0.20f, "공격력"),
        new("Umbreon",  Stat.CritChance,  0.10f, "치명타"),
        new("Leafeon",  Stat.MaxHp,       0.20f, "체력"),
        new("Glaceon",  Stat.ManaRegen,   0.30f, "마나회복"),
        new("Sylveon",  Stat.SpellPower,  0.30f, "주문력"),
    };

    /// <summary>총 소환 가능 종 수(=8).</summary>
    public static int Count => Entries.Length;

    // ─────────────────────────────────────────
    // 성급별 스킬 마나코스트 — "몇 종까지 도달하는가"를 정하는 손잡이
    // ─────────────────────────────────────────
    //
    // 기획 의도(2026-08-19): 1성·2성은 전투 시간 안에 8종을 다 못 모으는 게 정상이고,
    // <b>3성이 되어야만</b> 8종이 전부 나온다. 성급을 올릴 이유를 만드는 장치다.
    //
    // 계산 근거 — 마나는 초당 10 고정(BattleManager.MANA_PER_SECOND)이고 전투는
    //   정규 30초(MAX_TICKS 300 × TICK_INTERVAL 0.1) + 오버타임(5초 × 2배속 = 시뮬 10초)
    //   = <b>최대 40 시뮬레이션 초</b>다.
    // 8종 전부 스킬 시전으로만 나온다(전투 시작 즉발 소환 없음 — 기획 확정 2026-08-19).
    //
    //   마나 80 → 40초 안에 5회 시전 → 5종
    //   마나 60 → 6회                → 6종
    //   마나 30 → 8회(24초)          → 8종  ← 3성만 완성, 16초 여유
    //
    // 실제로는 시전이 공격 쿨다운·사거리 진입에 걸려 조금씩 늦고 대부분의 전투는 40초 전에 끝나므로,
    // 위 숫자는 "이론상 상한"이다. 3성의 16초 여유가 그 지연을 흡수한다.
    private static readonly int[] _skillManaCostByStar = { 80, 60, 30 };

    /// <summary>
    /// 해당 성급의 나인이볼부스트 스킬 마나코스트. 범위 밖 성급은 가장 가까운 값으로 잘린다.
    /// 이브이 원본 스킬(Celebrate)의 60은 쓰지 않는다 — 위 계단을 만들기 위한 전용 값이다.
    /// </summary>
    public static int SkillManaCost(int starLevel)
    {
        int index = starLevel - 1;
        if (index < 0) index = 0;
        if (index >= _skillManaCostByStar.Length) index = _skillManaCostByStar.Length - 1;
        return _skillManaCostByStar[index];
    }
}
