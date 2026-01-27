using NUnit.Framework;
public class InitialTest
{
    [Test]
    public void FirstTest()
    {
        int expected = 2;
        int actual = 1 + 1;
        
        Assert.AreEqual(expected, actual);
    }
}