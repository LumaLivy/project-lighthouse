using System.Diagnostics.CodeAnalysis;

namespace LBPUnion.ProjectLighthouse.Types.Match
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum RoomState
    {
        /// <summary>
        ///     The room isn't doing anything in particular.
        /// </summary>
        Idle = 0,

        /// <summary>
        ///     The room is looking to join an existing room playing a specific slot.
        /// </summary>
        DivingIntoLevel = 1,

        /// <summary>
        ///     ???
        /// </summary>
        Unknown = 2,

        /// <summary>
        ///     The room is looking for other rooms to join.
        /// </summary>
        DivingIn = 3,

        /// <summary>
        ///     The room is waiting for players to join their room.
        /// </summary>
        DivingInWaiting = 4,
    }
}