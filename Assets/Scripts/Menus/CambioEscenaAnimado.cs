using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para cambiar de escena
using System.Collections;

public class CambioEscenaAnimado : MonoBehaviour {
    [Header("Configuración")]
    [SerializeField] private Animator AnimatorMenu; // El Animator que controla toda tu UI
    [SerializeField] private float tiempoDeAnimacion = 0.5f; // Cuánto dura tu animación de salida
    [SerializeField] private string nombreSiguienteEscena; // El nombre exacto de tu Escena 2

    // Esta función es la que pondrás en el OnClick() de tu botón
    private bool enTransicion;

    public void IrASiguienteMenu() {
        if (enTransicion) return; // doble clic: una sola carga de escena
        enTransicion = true;
        StartCoroutine(RutinaTransicion());
    }

    private IEnumerator RutinaTransicion() {
        // 1. Disparamos la animación de salida (los botones se van)
        AnimatorMenu.SetTrigger("Exit");

        // 2. Esperamos exactamente lo que dura la animación
        yield return new WaitForSeconds(tiempoDeAnimacion);

        // 3. Cargamos la nueva escena de forma "invisible"
        SceneManager.LoadScene(nombreSiguienteEscena);
    }
}