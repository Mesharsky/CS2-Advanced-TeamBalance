namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public class Player
    {
        public string ?PlayerName { get; set; }
        public ulong PlayerSteamID { get; set; }
        public int Team { get; set; }
        public int Score { get; set; }
    }
}
