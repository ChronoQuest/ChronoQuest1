// using System.Collections;
// using NUnit.Framework;
// using UnityEngine;
// using UnityEngine.SceneManagement;
// using UnityEngine.TestTools;

// public class MovementPlaygroundTests
// {
//     // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
//     // `yield return null;` to skip a frame.
//     [UnityTest]
//     public IEnumerator PlayerExistsInMovementPlayground()
//     {
//         // Load MovementPlayground scene
//         yield return SceneManager.LoadSceneAsync("01", LoadSceneMode.Single);

//         // Wait a single frame
//         yield return null;

//         // Attempt to find the Player object
//         GameObject player = GameObject.FindWithTag("Player");

//         Assert.IsNotNull(player, "Couldn't find an object with the Player tag in MovementPlayground");
//     }

//     [UnityTest]
//     public IEnumerator PlayerExperiencesGravity()
//     {
//         yield return SceneManager.LoadSceneAsync("01", LoadSceneMode.Single);
//         GameObject player = GameObject.FindWithTag("Player");
//         // Get the starting height of the player
//         float startHeight = player.transform.position.y;
//         // Wait 0.2 seconds (for player to fall)
//         yield return new WaitForSeconds(0.2f);
//         // Get height after waiting
//         float endHeight = player.transform.position.y;
//         Assert.Less(endHeight, startHeight, "Player did not fall as expected. Do they start on the ground?");
//     }

//     [UnityTest]
//     public IEnumerator PlayerCanMoveHorizontally()
//     {
//         yield return SceneManager.LoadSceneAsync("01", LoadSceneMode.Single);
//         GameObject player = GameObject.FindWithTag("Player");
//         float startX = player.transform.position.x;
//         var controller = player.GetComponent<PlayerPlatformer>();
//         // Simulate moving right
//         controller.horizontalInput = 1f;
//         yield return new WaitForSeconds(0.5f);
//         float endX = player.transform.position.x;
//         Assert.Greater(endX, startX, "Player did not fall as expected. Is there a wall immediately to the right?");
//     }

//     [UnityTest]
//     public IEnumerator PlayerCanJump()
//     {
//         yield return SceneManager.LoadSceneAsync("01", LoadSceneMode.Single);
//         GameObject player = GameObject.FindWithTag("Player");
//         var controller = player.GetComponent<PlayerPlatformer>();
//         Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
//         // Check that the player cannot immediately jump, as they start in the air
//         Assert.IsFalse(controller.isGrounded, "Player starts in the air, and should not be allowed to immediately jump");
//         // Allow player to fall
//         yield return new WaitForSeconds(2f);
//         // Assert that the ground check is actually working
//         Assert.IsTrue(controller.isGrounded, "Player should eventually be settled on the ground, but was not");

//         controller.TriggerJumpTest();

//         yield return null; 

//         Assert.Greater(rb.linearVelocity.y, 0, "Player should have jumped");
//     }
// }
