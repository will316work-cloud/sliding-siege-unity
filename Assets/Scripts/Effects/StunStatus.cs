namespace SlidingSiege
{
    /// Siren's broken-link stun: the enemy skips its abilities for the
    /// given number of enemy phases (CanAct vetoes acting).
    public class StunStatus : StatusEffect
    {
        public StunStatus(int turns = 1) : base(turns) { }

        public override bool PreventsAction => true;
    }
}
