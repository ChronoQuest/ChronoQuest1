using UnityEngine;
using System; 

/* enum of flags for player actions
- each position can either be on or off 
- bit position corresponds to an action (e.g. bit 0 corresponds to move)
- performing a left shift means the binary will move/switch on a new action
- 
*/

[Flags]
public enum PlayerAction
{
    None = 0, 
    Movement = 1 << 0,
    Jump = 1 << 1, 
    Dash = 1 << 2, 
    Attack = 1 << 3,
    Rewind = 1 << 4,
    Spell = 1 << 5, 
    WallJump = 1 << 6, 
    RainSpell = 1 << 7,
    SpikeHint = 1 << 8, 

    All = ~0 
}
