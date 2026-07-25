# Nahtloses Vertex-Displacement auf flachen Polyeder-Flächen

Zielgruppe: AI-Agenten, die weitere Körper für die DarkWaterScreensaver-Szenen
bauen (Würfel, Oktaeder, Tetraeder, Ikosaeder, Dodekaeder, Prismen, ...).
Referenzimplementierung: `dark-water-cube-interactive.html` (bereits behoben)
und `dark-water-octahedron.html` (Vorlage für dieses Dokument).

## 1. Das Problem

Ein Körper mit **flachen** Flächen (im Gegensatz zu Sphäre/Knoten, die überall
glatt gekrümmt sind) wird pro Fläche als eigenes Dreiecksgitter gebaut, jede
Fläche mit **ihrer eigenen, konstanten Flächennormale**. Zwei benachbarte
Flächen teilen sich entlang einer Kante dieselben Vertex-*Positionen*, aber
nicht dieselbe Normale.

Der Vertex-Shader verschiebt jeden Punkt fürs Wellenbild entlang seiner
Normale:

```glsl
vec3 displaced = position + normal * h;   // h = Wellenhöhe an diesem Punkt
```

Auf einer Kante wird derselbe Ausgangspunkt also in **zwei verschiedene
Richtungen** verschoben (je nach Fläche) — die zwei Flächen laufen entlang der
Kante auseinander. Bei starken Wellen oder Splash-Ripples wird das als
sichtbarer Spalt ohne Wasserfläche erkennbar.

Betrifft **nur** Körper aus flachen Polygonflächen. Sphäre und Knoten sind
davon nicht betroffen, da sie keine harten Kanten haben und die
Wellenverschiebung dort ohnehin auf einem durchgängig glatten Normalenfeld
arbeitet.

## 2. Die Lösung: gemittelte Verschiebungsrichtung

Nicht die Beleuchtung ändern (die läuft im Fragment-Shader ohnehin über
Bildschirmableitungen, siehe Abschnitt 4) — nur ein **zweites Richtungsfeld
speziell für die Verschiebung** einführen, das an Kanten und Ecken stetig
ist:

- Im Flächeninneren: normale Flächennormale.
- Auf einer Kante: **normierte Summe** der Normalen aller Flächen, die diese
  Kante teilen (bei einem konvexen Polyeder i. d. R. genau 2).
- Auf einer Ecke: normierte Summe der Normalen aller Flächen, die an dieser
  Ecke zusammentreffen.

Weil beide (oder mehr) angrenzenden Flächen für denselben geometrischen Punkt
danach **dieselbe** Richtung berechnen, verschieben sie ihn identisch — der
Spalt schließt sich exakt, für jede Wellenamplitude.

```
dispDir(Flächeninneres) = faceNormal
dispDir(Kante)          = normalize(faceNormalA + faceNormalB)
dispDir(Ecke)           = normalize(Σ faceNormal_i über alle anliegenden Flächen)
```

`normalize(v)` = `v / |v|`, also der Vektor auf Länge 1 skaliert.

## 3. Implementierung

### 3.1 Neues Vertex-Attribut

Zusätzlich zu `position` und `normal` ein Attribut `dispDir` anlegen:

```js
const geo = new THREE.BufferGeometry();
geo.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
geo.setAttribute("normal",   new THREE.Float32BufferAttribute(normals, 3));
geo.setAttribute("dispDir",  new THREE.Float32BufferAttribute(dispDirs, 3));
```

`normal` bleibt unverändert erhalten (wird u. a. vom `THREE.Raycaster` für
Splash-Trefferpunkte gebraucht). `dispDir` ist nur für den Vertex-Shader.

### 3.2 Vertex-Shader

Eine Zeile ändern:

```glsl
attribute vec3 dispDir;
...
vec3 displaced = position + dispDir * h;   // vorher: normal * h
```

### 3.3 dispDir beim Geometrie-Aufbau berechnen

Zwei Wege, je nach Aufwand:

**A — generisch (funktioniert für jeden konvexen Polyeder):**
Für jeden erzeugten Vertex alle Flächen sammeln, die ihn (näherungsweise)
enthalten — also alle Flächen, deren Ebene durch den Punkt geht — und deren
Normalen aufsummieren, dann normalisieren. Praktisch: eine `Map` von
gerundeter Position → Liste der Flächennormalen aufbauen, während die
Dreiecke erzeugt werden, danach pro eindeutiger Position mitteln. Robust,
aber etwas mehr Code.

**B — analytisch (schneller, wenn die Flächennormalen aus wenigen festen
Vorzeichen-Kombinationen bestehen, wie bei Oktaeder/Würfel/Tetraeder):**
Wenn die Flächennormalen achsenparallele Vorzeichen-Muster sind
(z. B. Oktaeder: `(±1,±1,±1)/√3`), lässt sich `dispDir` direkt aus den
Vorzeichen der Vertex-Koordinaten ableiten, ohne Nachbarschaftssuche:

```js
const eps = radius * 1e-6;                 // Tolerenz gegen Gleitkommarauschen
const sgn0 = (v) => (Math.abs(v) < eps ? 0 : Math.sign(v));

function pushDispDir(p) {
  const dx = sgn0(p.x), dy = sgn0(p.y), dz = sgn0(p.z);
  const len = Math.sqrt(dx * dx + dy * dy + dz * dz); // 1, √2 oder √3
  dispDirs.push(dx / len, dy / len, dz / len);
}
```

Warum das funktioniert (Herleitung am Oktaeder-Beispiel): Ein Punkt mit einer
Koordinate exakt 0 liegt auf einer Kante und gehört zu den zwei Flächen mit
entgegengesetztem Vorzeichen dieser Koordinate. In der Normalensumme hebt
sich diese Komponente weg (`+1` und `−1` mitteln sich zu 0`), die übrigen
zwei Komponenten bleiben mit vollem Vorzeichen stehen und werden auf Länge 1
normiert. Mit zwei Null-Koordinaten (Ecke) bleibt nur eine Achsenrichtung
übrig — das Mittel aller vier dort zusammentreffenden Flächennormalen.

Die Null-Koordinaten sind exakt, **wenn** die Gitterpunkte als
Linearkombination von Basisvertices erzeugt werden, deren Nullen exakt sind
(so wie beim baryzentrischen Aufbau in `makeOctahedronGeometry`). `eps` fängt
nur Gleitkomma-Rundungsfehler ab, keine echte geometrische Unsicherheit.

Weg B ist deutlich weniger Code, funktioniert aber nur, wenn sich die
Flächennormalen so einfach aus den Koordinatenvorzeichen ablesen lassen.
Beim Würfel sind es die 6 Achsenrichtungen `(±1,0,0)` etc. — dort greift ein
analoges, noch einfacheres Schema. Bei unregelmäßigeren Körpern
(Ikosaeder, Dodekaeder) ist Weg A der sicherere Standardweg.

## 4. Was NICHT geändert werden muss

- **Fragment-Shader-Beleuchtung:** Reflexion, Fresnel-Term (Fresnel = winkel-
  abhängige Reflexionsstärke, hier `pow(1 - dot(N,V), 5)`), Glanzlichter —
  all das nutzt eine Normale, die aus Bildschirmableitungen der
  interpolierten Weltposition berechnet wird (`cross(dFdx(vWorldPos),
  dFdy(vWorldPos))`), nicht das Vertex-Attribut. Diese Normale ist nach der
  Verschiebung automatisch korrekt und stetig — keine Anpassung nötig.
- **Ripple-/Wellenfunktionen** (`waveRaw`, `rippleHeight`, `totalDisp`)
  bleiben unverändert; nur *wohin* verschoben wird, ändert sich.
- **Raycasting** für Splash-Erkennung nutzt weiterhin `normal`, nicht
  `dispDir` — unverändert lassen.

## 5. Checkliste für einen neuen Körper

1. Flache Flächen einzeln mit ihrer konstanten Flächennormale aufbauen
   (analog `makeOctahedronGeometry`).
2. Prüfen: Ist der Körper konvex mit wenigen, achsenparallelen
   Normalen-Vorzeichen-Kombinationen? → Weg B (analytisch). Sonst → Weg A
   (Normalen pro eindeutiger Position sammeln und mitteln).
3. `dispDir`-Attribut zur `BufferGeometry` hinzufügen.
4. Im Vertex-Shader `normal * h` → `dispDir * h` ändern, `attribute vec3
   dispDir;` deklarieren.
5. Visuell verifizieren: Kamera nah an eine Kante, starken Splash direkt auf
   die Kante setzen (`spawnSplash` mit `strength = 1.0`) — bei korrekter
   Implementierung bleibt die Wasseroberfläche dort geschlossen, auch bei
   maximaler Ripple-Amplitude.

## 6. Zwei Stolperfallen bei Weg A (generisch)

Am `dark-water-truncated-octahedron.html`-Körper (6 Quadrate + 8 Sechsecke,
zwei unterschiedliche Normalen-Familien → Weg A statt Weg B) sind zwei
zusätzliche Fehlerquellen aufgetreten, die die Checkliste oben nicht
abdeckt.

### 6.1 Normale umdrehen ⇒ immer auch die Polygon-Wicklung umdrehen

Beim automatischen Ausrichten der Flächennormale nach außen reicht es
**nicht**, nur die Normale zu negieren:

```js
// FALSCH — Normale zeigt danach zwar nach außen, aber die
// Dreieckswicklung (von außen gesehen) bleibt im Uhrzeigersinn und
// wird vom Renderer per Backface-Culling verworfen: die Fläche
// verschwindet komplett.
if (n.dot(centroid) < 0) n.negate();
```

```js
// RICHTIG — mit der Normale auch die Eckenreihenfolge des Polygons
// umkehren, bevor daraus trianguliert wird. Erst dann stimmen Normale
// und Wicklungsrichtung wieder überein.
if (n.dot(centroid) < 0) {
  poly = poly.slice().reverse();
  n.negate();
}
```

Symptom, an dem sich das erkennen lässt: ganze Flächen fehlen komplett
(nicht nur an den Kanten beschädigt) — ein deutliches Zeichen für
Backface-Culling, nicht für ein Displacement-Problem.

### 6.2 Positions-Schlüssel nie mit `toFixed` bilden — `Math.round` verwenden

Für Weg A werden Vertex-Positionen dedupliziert, indem sie auf einen
String-Schlüssel gerundet werden. Naheliegend, aber fehlerhaft:

```js
// FALSCH
const keyOf = (p) => `${p.x.toFixed(4)}_${p.y.toFixed(4)}_${p.z.toFixed(4)}`;
```

Auf Kanten, die exakt in einer Koordinatenebene liegen (eine Koordinate
soll 0 sein), berechnen zwei angrenzende Flächen denselben Punkt über
unterschiedliche Gleitkomma-Pfade (verschiedene Zwischenschritte, z. B.
verschiedene Schwerpunkte als Fächer-Ursprung bei einer Fächer-
Triangulierung). Das Ergebnis liegt dann bei ±10⁻¹⁶ statt exakt 0.
`toFixed(4)` unterscheidet aber `"0.0000"` von `"-0.0000"` — zwei
**verschiedene** Schlüssel für denselben geometrischen Punkt. Die
Flächen-Mengen der beiden Seiten werden dadurch nicht zusammengeführt,
jede Seite behält nur ihre eigene Flächennormale als `dispDir` — Spalt
genau an dieser Kante, obwohl der Algorithmus dort eigentlich greifen
sollte.

```js
// RICHTIG — auf ein Gitter quantisieren statt auf eine Dezimalstellenzahl
// runden. Math.round bildet sowohl +1e-16 als auch -1e-16 auf die
// ganze Zahl 0 ab; der Template-String macht aus einer daraus
// resultierenden -0 wieder "0" — beide Seiten der Kante landen auf
// demselben Schlüssel.
const keyOf = (p) =>
  `${Math.round(p.x * 1e4)}_${Math.round(p.y * 1e4)}_${Math.round(p.z * 1e4)}`;
```

Gitterweite so wählen, dass sie deutlich feiner ist als der kleinste
tatsächliche Punktabstand im Mesh (sonst verschmelzen benachbarte, aber
unterschiedliche Vertices fälschlich zu einem Schlüssel), aber deutlich
gröber als das zu erwartende Gleitkommarauschen (typischerweise
10⁻¹³–10⁻¹⁵ bei Koordinaten in der Größenordnung 1–10). Ein Faktor
`1e4` (Gitterweite 10⁻⁴) ist für die meisten Körper in dieser Szenen-
Sammlung ein sicherer Wert.

Symptom, an dem sich das erkennen lässt: Spalte treten **nur** an
bestimmten Kanten auf (typischerweise denen in einer Koordinatenebene),
nicht an allen — anders als das ursprüngliche Problem aus Abschnitt 1,
das gleichmäßig an jeder Kante auftritt.

