using NUnit.Framework;
using UnityEngine;

namespace ProjectTests
{
    public class InGameMenuControllerTests
    {
        [Test]
        public void TestQuitToMainMenuResetsTimeScale()
        {
            // Thiết lập Time.timeScale khác 1f
            Time.timeScale = 0.5f;

            // Tạo GameObject giả lập
            GameObject go = new GameObject("TestMenuController");
            InGameMenuController controller = go.AddComponent<InGameMenuController>();

            // Thực thi (Vì LoadScene trong Unit Test EditMode có thể bị bỏ qua hoặc ném lỗi nếu cảnh không chạy,
            // nên ta dùng try-catch để tập trung kiểm tra việc đặt lại timeScale)
            try
            {
                controller.QuitToMainMenu();
            }
            catch (System.Exception ex)
            {
                Debug.Log($"[Test] Bỏ qua lỗi load scene trong chế độ test: {ex.Message}");
            }

            // Kiểm tra xem timeScale có được khôi phục về 1f hay không
            Assert.AreEqual(1f, Time.timeScale);

            // Dọn dẹp GameObject test
            Object.DestroyImmediate(go);
            Debug.Log("[Test] TestQuitToMainMenuResetsTimeScale: PASS - Time.timeScale đã khôi phục về 1f.");
        }
    }
}
