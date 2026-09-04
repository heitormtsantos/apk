namespace BattleRoyale
{
    public readonly struct LootTransferResult
    {
        public int AcceptedAmount { get; }
        public int RemainingAmount { get; }
        public int Accepted => AcceptedAmount;
        public int Remaining => RemainingAmount;
        public bool Success { get; }
        public string FailureReason { get; }

        private LootTransferResult(int acceptedAmount, int remainingAmount, bool success, string failureReason)
        {
            AcceptedAmount = acceptedAmount;
            RemainingAmount = remainingAmount;
            Success = success;
            FailureReason = failureReason ?? string.Empty;
        }

        public static LootTransferResult Transferred(int acceptedAmount, int remainingAmount) =>
            new(acceptedAmount, remainingAmount, acceptedAmount > 0, string.Empty);

        public static LootTransferResult Failed(int remainingAmount, string reason) =>
            new(0, remainingAmount, false, reason);
    }
}
