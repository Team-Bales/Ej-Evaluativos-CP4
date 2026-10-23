# Entrega en LaTeX

El PDF de la entrega, `CP4_Bales.pdf`, se genera desde esta carpeta. Cada issue de inciso o restricción entrega **un fragmento `.tex`** con su solución, y `main.tex` los une.

## Cómo compilar

```sh
cd latex
latexmk -pdf -jobname=CP4_Bales main.tex
```

Sin `latexmk`, ejecuta `pdflatex -jobname=CP4_Bales main.tex` dos veces (la segunda vez genera el índice). Los incisos que todavía no tienen fragmento aparecen como *Pendiente* y el PDF compila igual.

## Dónde va cada fragmento

| Issue | Fichero |
|---|---|
| #1 E9-a | `ejercicio-09-notificaciones-bolsa/latex/inciso-a.tex` |
| #2 E9-b | `ejercicio-09-notificaciones-bolsa/latex/inciso-b.tex` |
| #3 E9-c | `ejercicio-09-notificaciones-bolsa/latex/inciso-c.tex` |
| #4 E9-d | `ejercicio-09-notificaciones-bolsa/latex/inciso-d.tex` |
| #5 E9-R | `ejercicio-09-notificaciones-bolsa/latex/restriccion.tex` |
| #6 E12-a | `ejercicio-12-transacciones-bancarias/latex/inciso-a.tex` |
| #7 E12-b | `ejercicio-12-transacciones-bancarias/latex/inciso-b.tex` |
| #8 E12-c | `ejercicio-12-transacciones-bancarias/latex/inciso-c.tex` |
| #9 E12-d | `ejercicio-12-transacciones-bancarias/latex/inciso-d.tex` |
| #10 E12-R | `ejercicio-12-transacciones-bancarias/latex/restriccion.tex` |

## Reglas para los fragmentos

- **Sin preámbulo:** nada de `\documentclass`, `\usepackage` ni `\begin{document}`. Los paquetes van en [`preambulo.tex`](preambulo.tex); si te falta uno, añádelo allí.
- **Sin título de inciso:** `main.tex` ya pone `\subsection{a) Patrón}`, etc. Dentro del fragmento usa `\subsubsection` y `\paragraph`.
- **Imágenes** con ruta desde la raíz del repo:
  ```latex
  \begin{figure}[H]
    \centering
    \includegraphics[width=\textwidth]{ejercicio-12-transacciones-bancarias/diagramas/mi-diagrama.png}
    \caption{Diagrama de clases de la solución.}
  \end{figure}
  ```
- **Código C#** con `lstlisting` (el estilo ya está configurado). Solo los fragmentos relevantes; el código completo se queda en el repo:
  ```latex
  \begin{lstlisting}
  public interface ICuenta { decimal Saldo { get; } }
  \end{lstlisting}
  ```
- **Tablas** con `booktabs` (`\toprule`, `\midrule`, `\bottomrule`).
- El fragmento debe tener el **mismo contenido** que la respuesta en Markdown del inciso (por ejemplo, `inciso-b.md`), que se mantiene para leerla en GitHub.
- Antes de cerrar el issue, comprueba que `main.tex` compila sin errores con tu fragmento.
