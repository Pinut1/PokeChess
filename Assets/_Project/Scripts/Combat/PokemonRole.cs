/// <summary>
/// PokemonData.role 문자열 상수 모음(실측 6종: Tanker/Warrior/Assassin/Magician/Archer/Supporter).
/// enum으로 강제 변환하지 않는 이유: role은 시트에서 그대로 들어오는 문자열이라 오타가 있어도
/// 임포트는 통과해야 하고, 타겟팅 쪽은 알 수 없는 값을 안전하게 폴백해야 한다.
/// </summary>
public static class PokemonRole
{
    public const string Tanker    = "Tanker";
    public const string Warrior   = "Warrior";
    public const string Assassin  = "Assassin";
    public const string Magician  = "Magician";
    public const string Archer    = "Archer";
    public const string Supporter = "Supporter";
}
