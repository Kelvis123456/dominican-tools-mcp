# Decisiones

Cada entrada dice qué elegí, qué descarté y por qué.

## 2026-11-04 · Copiar el código, no hacer un paquete compartido

**Elegí:** copiar los validadores de dgii-helper y la búsqueda de codigo-trabajo-rag dentro del servidor, con su origen dicho en el README.

**Descarté:** publicar los tres como paquetes NuGet o armar un repositorio común. Serían dos paquetes más que mantener para unas 300 líneas que casi no cambian, y no quería que este proyecto dependiera de que los otros dos se publiquen. El costo es real: si arreglo algo allá, hay que traerlo aquí a mano. Lo dice el README.

## 2026-11-04 · stdio, y sin autenticación

El servidor corre como un proceso local que lanza quien lo usa, así que no hay red de por medio y no necesita autenticación. Publicarlo por HTTP sí la necesitaría, y no lo hice. Los logs van a stderr porque stdout es del protocolo: un `Console.WriteLine` suelto lo rompería.

## 2026-11-04 · Una entrada inválida es una respuesta; un uso incorrecto es un error

Un RNC con el dígito verificador mal es información que el modelo necesita (`valid: false, problem: CheckDigit`), así que vuelve como resultado normal. Un valor de 65 caracteres o un `top` de 0 son un uso incorrecto de la herramienta y salen como error (`McpException`), que es lo que el modelo sabe tratar como "reintenta con otros argumentos". Hay pruebas de las dos cosas. Un parámetro obligatorio ausente lo convierte el SDK en error de la herramienta, no en excepción del cliente; lo averigüé porque mi primera prueba esperaba una excepción.

Las respuestas de los validadores nunca repiten el valor recibido. Así una cadena hostil metida en un identificador no vuelve al contexto del modelo como si fuera una instrucción.

## 2026-11-04 · La descripción de cada herramienta dice lo que no hace

El modelo elige herramientas por su descripción. "Valida un RNC" invita a usarla para saber si una empresa existe; "solo comprueba el formato y el dígito verificador, no consulta a la DGII" no. Una prueba exige que esa frase esté. La descripción de la búsqueda avisa que es léxica y que prefiere palabras de la ley a preguntas largas.

## 2026-11-04 · Dos capas de evaluación, y una solo a mano

**Determinista (`golden`):** casos con respuesta conocida contra el servidor real. Corre en el CI y falla si algo deja de dar lo correcto. Los vectores de identificadores los calculé a mano en dgii-helper y las preguntas de búsqueda son las 21 que medí en el RAG.

**Con un modelo (`agent`):** mide si Gemini elige la herramienta correcta y responde con lo que ella devolvió. No corre en el CI: gasta cuota, necesita una clave y no es determinista.

**Los `knownMiss`:** la búsqueda por palabras no resuelve 7 de las 21 preguntas. Los marqué como fallos esperados en vez de quitarlos del conjunto. Si un cambio los arregla, el reporte dice "mejora" y devuelve un código de salida distinto de cero hasta que quito la marca; así una mejora no pasa desapercibida y el conjunto de evaluación no se queda desactualizado.

## 2026-11-04 · Lo que esta evaluación no demuestra

La del agente tiene 20 prompts que escribí yo, un modelo pequeño, una corrida y comprobaciones de texto en vez de un juez. Que 19 de 19 salgan bien dice que las descripciones no confundieron a ese modelo en esos prompts; no dice nada sobre otros modelos, otras formulaciones o ataques más trabajados.

La comprobación de "números sin fuente" dio una alerta que era falsa: el artículo 203 escribe «ciento por ciento» y el modelo escribió «100 %». La dejé así y lo conté en el README en vez de ajustar la heurística hasta que diera cero.

La primera corrida se cortó a los 8 prompts por la cuota del plan gratis de Gemini. Agregué espera y reintento por cuota, y conservé los dos resultados.

## 2026-11-04 · Qué descubrió construirlo

Esperaba que `search_labor_code("preaviso desahucio")` devolviera el artículo 76 y no lo hace ni con la pregunta completa. El RAG ya lo medía (era uno de sus fallos con palabras), pero verlo desde el lado de la herramienta lo hace más claro: para un modelo que llama a esta herramienta, ese fallo es silencioso, porque devuelve cinco artículos plausibles y ninguno es el que hace falta. La mitigación honesta es la descripción; la real, la búsqueda por vectores.
