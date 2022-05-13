<p align="center">[🇺🇸 🇬🇧 English README](https://github.com/VRCBilliards/vrcbce/blob/master/README.md) | [🗾 🇯🇵 Japanese README](https://github.com/VRCBilliards/vrcbce/blob/master/README_ja.md)</p>

<p align="center"><img src="https://avatars.githubusercontent.com/u/50210138?s=200&v=4" alt="Prefabs Logo"></p>

<p align="center"><i>Este prefab no podría haberse creado sin el amable apoyo de la comunidad de VRCprefabs. <3</i></p>

![Header](https://user-images.githubusercontent.com/6299186/136136789-f195e2ef-0cce-4807-8313-f62c39159b2f.png)

Una mesa de billar para mundos de VRchat SDK3. ¿Quieres jugar a 8 bolas, 9 bolas o 4 bolas japonesa/coreana? ¡Este es el prefab para ti! ¡Con el poder de la actualización de Udon Networking, incluso puedes tener varias mesas en el mismo mundo sin problemas!

Este prefab existe como una "Edición comunitaria" de la mesa de billar ht8b original. Simplifica mucho el código y facilita la edición. También se proporciona bajo el MIT, y los mantenedores de este código base se comprometen a ser abiertos e inclusivos para cualquier persona que desee modificar el prefab, agregar modos adicionales, corregir errores y utilizar el prefab como herramienta de aprendizaje. ¡Recomendamos **encarecidamente** a cualquiera que pruebe a modificar y/o contribuir a este prefab!

Este prefab no tiene limitaciones en cuanto a su uso. Puede ser:

- ¡Colocado en cualquier lugar de la escena!
- ¡Tener cualquier rotación!
- ¡Tener cualquier escala!
- ¡Estar en una plataforma giratoria!
- Usado repetidamente, dentro de lo razonable.
- Ser usado tanto en PC como en mundos Quest (tenga en cuenta que este prefab puede ser un poco pesado para la CPU en Quest)

También es 100% libre de modificar, reutilizar y redistribuir. ¡Hazlo tuyo!

Si desea ponerse en contacto con los mantenedores del repositorio:

[@FairlySadPanda](https://twitter.com/FairlySadPanda) en Twitter,
FairlySadPanda#9528 en Discord

[@Metamensa](https://twitter.com/Metamensa) en Twitter,
Metamaniac#3582 en Discord

# Instalación

Requisitos:

1. Un proyecto con la última versión de VRChat SDK3 instalada
2. El proyecto también tiene que tener el [UdonSharp](https://github.com/MerlinVR/UdonSharp) más reciente 
3. El proyecto también tiene que tener instalado TextMeshPro.

Recomendado:

1. [CyanEmu](https://github.com/CyanLaser/CyanEmu) para emular localmente
2. [VRWorldToolkit](https://github.com/oneVR/VRWorldToolkit) para asistencia general para el desarrollo del mundo

Pasos de instalación:

1. [Descargue la versión mas reciente del paquete unity](https://github.com/noch3d/vrcbce-spanish/releases/latest).
2. Importe el paquete a unity.
3. Dentro de la carpeta VRCBilliardsCE, seleccione cualquiera de los prefabs de mesas, arrástrelo y suéltelo en la escena.
4. ¡Y listo!

# Integración de UdonChips

¡Con 1.2.1, VRCBCE admite [UdonChips](https://lura.booth.pm/items/3060394)!

Para habilitar la compatibilidad con UdonChips, debe hacer dos cosas:

  1. Tenga un UdonChips UdonBehaviour en el proyecto, con un objeto al que se llama "UdonChips".
  2. Marque la opción "Habilitar UdonChips" en el objeto VRCBilliards de su mesa de billar.

El objeto VRCBilliards, que contiene el script principal PoolStateManager, contiene varias opciones. Por el momento, se admite lo siguiente:

  1. Pagar UC para unirse a un juego de billar.
  2. Si Permitir incrementos está habilitado, puede pagar para unirse varias veces: ¡cuanto más pague, más podrá pagar la mesa!
  3. También puedes ganar UC por ganar contra ti mismo.
  4. Todos los costos y recompensas se pueden modificar a través del script PoolStateManager.
  5. El mensaje exacto que se muestra en cada botón para unirse se puede personalizar en el script PoolMenu.

# Obtener soporte

A menos que sea urgente, ¡no envíe mensajes directos a los colaboradores de VRCBCE para pedir ayuda!

La mejor manera de obtener soporte es crear un issue. Necesitará una cuenta de GitHub para esto, que tarda menos de un minuto en configurarse.

Luego, haga clic en issues en la parte superior de esta página:

![imagen](https://user-images.githubusercontent.com/732532/127752254-37061d3a-c13e-4de7-9212-792e17fe6472.png)

Luego haga clic en Crear issue.

![imagen](https://user-images.githubusercontent.com/732532/127752268-c46fca03-72cf-4712-96b9-24e47764d791.png)

Luego, agregue su informe de error o problema en el cuadro y haga clic en Enviar nuevo issue.

![imagen](https://user-images.githubusercontent.com/732532/127752457-03751bba-df2b-48f0-a220-a9cd699d9974.png)

Enviar un DM a un colaborador puede brindarle una respuesta más rápida, pero escribir un issue significa que todos los colaboradores pueden ver el problema, se pueden rastrear y hacer referencia a los errores y, en general, ¡es mucho más fácil arreglar las cosas!

# Realización de solicitudes de pull a este repositorio

El código de este proyecto está escrito para parecerse al código normal de Unity/C#. C# tiene varios estándares (y los equipos tienden a establecer los suyos propios), pero como referencia, consulte la documentación de Unity, los scripts de ejemplo de Unity y las [directrices de mejores prácticas de Microsoft](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).

  En general:
  - Poner variables en la parte superior del behaviour.
  - Evite el uso de guiones bajos antes de las propiedades y los métodos, a menos que sea un método público que deba ser no compatible con RPC por razones de seguridad (un uso específico de Udon del guión bajo).
  - Use camelCase para propiedades y argumentos, y use PascalCase para todo lo demás.

# Acreditación en tus Mundos

Como VRCBCE es un proyecto complejo de varias capas, hemos recibido ayuda desde muchos lugares a lo largo de su desarrollo. A medida que aumenta la cantidad de personas que contribuyen a este proyecto, es justo para todos los involucrados que el grupo en su conjunto obtenga representación cuando se le acredita en los mundos. La forma más fácil e inclusiva de acreditarnos sería acreditar a la organización "VRCBilliards", o tal vez podría escribir "Equipo VRCBCE" o algo así. Si ABSOLUTAMENTE insiste en nombrar nombres: "FairlySadPanda & Table" funciona como mínimo, y es **muy recomendable** que también acredite al creador del modelo de mesa que está utilizando (el nombre del creador de la interfaz de usuario ya está incluido en la sección de información de cada mesa, ¡pero no está de más volver a acreditarlos también!). Otros contribuyentes al proyecto se pueden encontrar en los créditos a continuación.

# Creador Original

El creador original de este prefab fue harry_t. harry_t intentó (sin éxito) usar DMCA para este repositorio fuera de Github, pero no se dio cuenta de que estaban publicando exactamente los mismos assets en su propio Github como dominio público. Actualmente están PEA(perdido en acción) después de bombardear su Github/Twitter. A pesar de esto, es justo citarlo como la fuente original y dar crédito al impresionante código de física que impulsa todo este prefab. También hizo una pequeña contribución directamente a este repositorio.


# Creditos
🐼 FairlySadPanda - Maintainer, Lead Programmer, Networking, Refactoring

😺 Table - Maintainer, Designer, Optimization, General polish, QA

✨ esnya - UI, UdonChips implementation, misc. fixes

🌙 M.O.O.N - UI

🌳 Ivylistar - Metal Table

🦊 Juice - CottonFox Table

🦈 akalink - Classic Table, UI, Color Change shaders

🚗 Varneon - Optimization

🧙‍♂️ Xiexe - Original Forker, Early refactor work

🧙‍♀️ Silent - [Filamented](https://gitlab.com/s-ilent/filamented)

🎨 Floatharr & Synergiance - Textures

💻 Vowgan & Legoman99573 - Misc. commits
 
🐆 Noch - Spanish README translation

harry_t - Original Prefab, Physics code
