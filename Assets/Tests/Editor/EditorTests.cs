using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class EditorTests
{
    [Test]
    public void ValidSprite()
    {
        // WARNING : This filepath will need to change when we update the sprite used for main player 
        GameObject playerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sprites/MeleeSkeleton/Skull Boy.aseprite");
        Assert.IsNotNull(playerSprite, "Couldn't find player sprite");
    }

    [Test]
    public void ValidPlayerMovementScript()
    {
        MonoScript script = UnityEditor.AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/PlayerMovement.cs");
        Assert.IsNotNull(script, "Couldn't find script for player movement!");
    }

    [Test]
    public void PlayerMovementHasSerializedFields()
    {
        var go = new GameObject();
        var player = go.AddComponent<PlayerPlatformer>();

        var type = player.GetType();

        var moveSpeedField = type.GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var jumpForceField = type.GetField("jumpForce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var groundCheckField = type.GetField("groundCheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var groundCheckRadiusField = type.GetField("groundCheckRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var groundLayerField = type.GetField("groundLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var jumpBufferTimeField = type.GetField("jumpBufferTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(moveSpeedField, "moveSpeed field missing!");
        Assert.IsTrue(moveSpeedField.IsDefined(typeof(SerializeField), false), "moveSpeed should be [SerializeField]!");

        Assert.IsNotNull(jumpForceField, "jumpForce field missing!");
        Assert.IsTrue(jumpForceField.IsDefined(typeof(SerializeField), false), "jumpForce should be [SerializeField]!");

        Assert.IsNotNull(groundCheckField, "groundCheck field missing!");
        Assert.IsTrue(groundCheckField.IsDefined(typeof(SerializeField), false), "groundCheck should be [SerializeField]!");

        Assert.IsNotNull(groundCheckRadiusField, "groundCheckRadius field missing!");
        Assert.IsTrue(groundCheckRadiusField.IsDefined(typeof(SerializeField), false), "groundCheckRadius should be [SerializeField]!");

        Assert.IsNotNull(groundLayerField, "groundLayer field missing!");
        Assert.IsTrue(groundLayerField.IsDefined(typeof(SerializeField), false), "groundLayer should be [SerializeField]!");

        Assert.IsNotNull(jumpBufferTimeField, "jumpBufferTime field missing!");
        Assert.IsTrue(jumpBufferTimeField.IsDefined(typeof(SerializeField), false), "jumpBufferTime should be [SerializeField]!");
    }
}
