% Hekatan Lab / MATLAB (Symbolic) — cópialo a Hekatan Lab: texto, operación y gráfica igual.
% Para MEDIR el tiempo, rodéalo con  tic ... toc  (en LISP: get-internal-real-time).
syms EI L M N P V a0 a1 a2 a3 c0 c1 d dM dV dx q s x
%" De dónde sale la rigidez de una barra: EI y L, término a término
%' Aquí se DEDUCE de cero, empezando por un trocito de viga: equilibrio → ecuación de la viga → forma cúbica → funciones de forma → curvaturas → energía → integral. Con @{EI} y @{L} en símbolos, cada número sale de una cuenta. Todo con el motor.

%" 1 · El comienzo: el equilibrio de un trocito de viga
%' Todo empieza aquí. Corto un pedacito de viga de ancho @{dx} y le miro las fuerzas. En la cara izquierda actúan el momento @{M} y el cortante @{V}; en la derecha, un poco cambiados: @{M + dM} y @{V + dV}. Encima, la carga repartida @{q}. El trocito está quieto, así que fuerzas y momentos se equilibran:
figure; hold on
fill([0 1 1 0],[0 0 0.5 0.5],[0.92 0.89 0.82],'EdgeColor',[0.2 0.2 0.2],'LineWidth',1.5)
qc = [0.85 0.35 0.19];
plot([0 1],[0.72 0.72],'color',qc,'LineWidth',1.2)
qx = 0.08:0.15:0.96;
for i=1:numel(qx); quiver(qx(i),0.72,0,-0.18,0,'color',qc,'LineWidth',1,'MaxHeadSize',3); end
text(0.46,0.83,'q','color',qc,'FontWeight','bold')
bc = [0.17 0.4 0.77];
quiver(0,-0.16,0,0.56,0,'color',bc,'LineWidth',1.6,'MaxHeadSize',0.4)
text(-0.09,0.22,'V','color',bc,'FontWeight','bold')
quiver(1,0.40,0,-0.56,0,'color',bc,'LineWidth',1.6,'MaxHeadSize',0.4)
text(1.04,0.22,'V+dV','color',bc,'FontWeight','bold')
th = linspace(-0.6, -0.6+1.5*pi, 40); r = 0.075;
plot(0+r*cos(th), 0.33+r*sin(th), 'color',bc,'LineWidth',1.5)
plot(1-r*cos(th), 0.33+r*sin(th), 'color',bc,'LineWidth',1.5)
text(-0.12,0.46,'M','color',bc,'FontWeight','bold')
text(1.02,0.46,'M+dM','color',bc,'FontWeight','bold')
gc = [0.5 0.5 0.5];
plot([0 1],[-0.28 -0.28],'color',gc); plot([0 0],[-0.24 -0.32],'color',gc); plot([1 1],[-0.24 -0.32],'color',gc)
text(0.47,-0.37,'dx','color',[0.3 0.3 0.3])
plot([-0.55 -0.30],[0.25 0.25],'color',[0.3 0.3 0.3]); quiver(-0.32,0.25,0.04,0,0,'color',[0.3 0.3 0.3],'MaxHeadSize',5)
text(-0.26,0.25,'x','color',[0.3 0.3 0.3])
title('equilibrio del trocito (dx)')
axis([-0.6 1.35 -0.45 0.92]); axis equal; axis off
figure;
%' Dos condiciones de equilibrio, y de cada una sale una ley. Sumo las fuerzas verticales de las dos caras más la carga (todo el trocito, y vale cero):
Fy = V - (V + dV) - q*dx   % fuerzas verticales = 0
%' Se cancela @{V} y sobra el cambio @{dV} contra la carga @{q}: el cortante cae al ritmo de la carga. Ahora sumo los momentos respecto a una cara (también cero, los términos con dx·dx se desprecian):
Mo = M + dM - M - V*dx   % momentos = 0
%' Se cancela @{M} y sobra el cambio @{dM} contra el cortante @{V}: el momento crece al ritmo del cortante.

%" 2 · Qué es v (la deflexión) y la ecuación de la viga
%' Primero, qué es v. La deflexión v es cuánto BAJA el eje de la viga en cada punto x — la forma que toma al cargarse. Es la INCÓGNITA de todo el problema. OJO: v (minúscula, la deflexión) NO es V (mayúscula, el cortante del paso 1); son cosas distintas.
figure
xg = linspace(0,1,150);
plot([0 1],[0 0],'k--'); hold on
plot(xg, -0.15*sin(pi*xg), 'b', 'LineWidth', 2)
text(0.32,-0.17,'v(x)')
title('la deflexion v(x)'); xlabel('x'); axis off
figure;
%' Sus derivadas tienen nombre propio: la 1ª, v′, es el giro (la pendiente del eje); la 2ª, v″, es la curvatura (cuánto se dobla); y la 4ª, v⁗, es la que aparece en la ecuación de la viga.
%' Ahora ligo el momento @{M} con esa forma, en dos pasos. Geometría (flechas pequeñas): la curvatura es la segunda derivada de la deflexión — v″. Material y sección: por Hooke cada fibra tira según su distancia al eje; al sumar esas tensiones por su brazo, el momento @{M} es la rigidez @{EI} por la curvatura (la @{EI} reúne el módulo E y la inercia de la sección).
%' Ahora junto las tres leyes. Del paso 1: la pendiente del momento es el cortante @{V}, y la pendiente del cortante es menos la carga @{q}. Y acá: el momento @{M} es @{EI} por la curvatura. Encadenándolas, la carga @{q} queda igual a @{EI} por la CUARTA derivada de la deflexión: esa es la ecuación de la viga.
%' DENTRO del tramo, entre nudo y nudo, no hay carga repartida: la carga @{q} vale cero. Entonces la cuarta derivada de la deflexión es cero. Eso es lo que resuelvo, con el motor, en el paso 3.

%" 3 · Una viga de verdad: el voladizo, resuelto con L
%' Para ver el mecanismo con FÓRMULAS reales, resuelvo primero una viga concreta de largo @{L}: un voladizo (empotrado a la izquierda, libre a la derecha) con una carga @{P} en la punta. Por estática, el momento a una distancia @{x} del empotramiento es:
Mvol = -P*(L - x)   % momento del voladizo
%' La ecuación de la viga dice que @{EI} por la curvatura es el momento. Integro el momento una vez y sale @{EI} por el giro. El empotramiento no deja girar en el arranque (el giro en @{x}=0 es cero), así que la constante de integración es cero:
EIvp = int(-P*(L - x), x)   % EI·v′, giro × EI; la +C = 0
%' Integro otra vez y sale @{EI} por la deflexión. El empotramiento tampoco deja bajar en el arranque (la deflexión en @{x}=0 es cero), así que otra vez la constante es cero:
EIv = int(-P*L*x + P*x^2/2, x)   % EI·v, deflexión × EI; la +C = 0
%' @{EIv} dividido por @{EI} es la deflexión. En la punta (@{x}=@{L}) da la fórmula clásica del voladizo, @{vpunta}:
vpunta = -P*L^3/(3*EI)   % flecha en la punta
%' OJO al resultado: la deflexión salió un polinomio de grado 3 — una CÚBICA en @{x}. No es casualidad: sin carga dentro del tramo, la ecuación es @{EI}·v⁗ = 0 y su solución es SIEMPRE una cúbica (4 constantes). Aquí las fijó el empotramiento; en un elemento de dos nudos las fijan los 4 datos de nudo (descenso y giro en cada punta). Eso es el paso 4.

%" 4 · La elástica de Timoshenko, la cúbica y las funciones de forma
%' La regla de oro de la viga (Timoshenko) liga la FORMA con el momento: la rigidez por la curvatura es el momento, @{EI}·v″ = M. Uso un voladizo con una carga P en la punta, y voy paso a paso — cada diagrama CON su viga arriba, integrando (cada integral sube un grado).
%' 1) La CARGA. ¿Qué es @{q}=0? El elemento sólo recibe fuerzas en sus NUDOS (los extremos): la P está EN la punta, no repartida a lo largo de la viga. Por eso, a lo largo del tramo, la carga repartida es cero, @{q}=0. No es un supuesto al azar: es lo que define al elemento. En el diagrama, una línea plana:
figure; hold on
plot([0 1],[1.3 1.3],'k','LineWidth',3)
plot([0 0],[1.1 1.5],'k','LineWidth',2)
quiver(1,1.7,0,-0.32,0,'r','LineWidth',2); text(1.03,1.75,'P')
text(-0.28,1.3,'viga')
xg = linspace(0,1,120);
plot([0 1],[0 0],'color',[0.6 0.6 0.6])
plot(xg, 0*xg, 'b','LineWidth',2)
text(-0.28,0,'q=0')
axis([-0.32 1.15 -0.6 1.95]); axis off
title('la carga q = 0 (sin carga)')
figure;
%' 2) El CORTANTE es la integral de −q (ley dV/dx=−q). Integrar cero da una CONSTANTE — el cortante no cambia (franja plana):
Vcte = int(0*x, x)   % V = int(-q): constante
figure; hold on
plot([0 1],[1.3 1.3],'k','LineWidth',3)
plot([0 0],[1.1 1.5],'k','LineWidth',2)
quiver(1,1.7,0,-0.32,0,'r','LineWidth',2); text(1.03,1.75,'P')
text(-0.28,1.3,'viga')
xg = linspace(0,1,120);
plot([0 1],[0 0],'color',[0.6 0.6 0.6])
plot(xg, -0.35+0*xg, 'b','LineWidth',2)
text(-0.28,0,'V')
axis([-0.32 1.15 -0.6 1.95]); axis off
title('el cortante V (constante)')
figure;
%' 3) El MOMENTO es la integral del cortante (ley dM/dx=V). Integrar una constante da una RECTA (un triángulo):
Mrecta = int(c1, x)   % M = ∫V = c1·x + c0, recta
figure; hold on
plot([0 1],[1.3 1.3],'k','LineWidth',3)
plot([0 0],[1.1 1.5],'k','LineWidth',2)
quiver(1,1.7,0,-0.32,0,'r','LineWidth',2); text(1.03,1.75,'P')
text(-0.28,1.3,'viga')
xg = linspace(0,1,120);
plot([0 1],[0 0],'color',[0.6 0.6 0.6])
plot(xg, -0.42*(1-xg), 'b','LineWidth',2)
text(-0.28,0,'M')
axis([-0.32 1.15 -0.6 1.95]); axis off
title('el momento M (recta)')
figure;
%' 4) La DEFLEXIÓN. La elástica manda integrar el momento (la recta @{c0}+@{c1}·@{x}) dos veces más: una vez → PARÁBOLA (es @{EI}·v′), otra vez → CÚBICA (es @{EI}·v):
EIv1 = int(c0 + c1*x, x)   % EI·v′: parábola
EIv2 = int(c0*x + c1*x^2/2, x)   % EI·v: cúbica
figure; hold on
plot([0 1],[1.3 1.3],'k','LineWidth',3)
plot([0 0],[1.1 1.5],'k','LineWidth',2)
quiver(1,1.7,0,-0.32,0,'r','LineWidth',2); text(1.03,1.75,'P')
text(-0.28,1.3,'viga')
xg = linspace(0,1,120);
plot([0 1],[0 0],'color',[0.6 0.6 0.6])
plot(xg, -0.42*(3*xg.^2-xg.^3)/2, 'b','LineWidth',2)
text(-0.28,0,'v')
axis([-0.32 1.15 -0.6 1.95]); axis off
title('la deflexion v (cubica)')
figure;
%' Ahí está: @{EIv2} es de GRADO 3. Mira sus coeficientes: cada uno es una mezcla de @{c0}, @{c1}, @{EI} y las constantes de integración. Pero esos valores exactos NO me importan todavía. Lo único que importa es que es una CÚBICA con 4 números libres.
%' Así que a esos 4 números, valgan lo que valgan, les pongo letras genéricas @{a0}, @{a1}, @{a2}, @{a3} — es como decir que 3·x + 4·x² lo escribes a·x + b·x². No estoy inventando: sólo cambio los coeficientes enredados por letras limpias, que voy a FIJAR después con las 4 condiciones de nudo. Y paso a la coordenada @{s} = @{x/L} (de 0 a 1):
vcubica = a0 + a1*s + a2*s^2 + a3*s^3   % la cúbica con 4 letras por fijar
%' Ahora, las funciones de forma. Cada una es esa cúbica cuando UN dato de nudo vale 1 y los otros tres 0; multiplicando cada forma por su dato de nudo y sumando, se arma cualquier deformada. Las 4 constantes se fijan con los 4 datos de nudo. Necesito también la pendiente de la cúbica:
vslope = diff(a0 + a1*s + a2*s^2 + a3*s^3, s)   % pendiente dv/ds
%' OJO con el giro: como @{s} = @{x/L}, el giro físico es θ = @{1/L}·dv/ds; por eso un giro unitario pide una pendiente @{L} en @{s}, y las funciones de giro llevan @{L}.
%' N1 — descenso unitario en el izquierdo. En @{s}=0: la cúbica vale 1 y no gira → @{a0}=1, @{a1}=0. En @{s}=1: vale 0 y no gira → 1+@{a2}+@{a3}=0 y @{a1}+2·@{a2}+3·@{a3}=0 → @{a3}=2, @{a2}=−3:
N1 = 1 - 3*s^2 + 2*s^3   % descenso izquierdo
%' N2 — giro unitario en el izquierdo: no baja pero gira 1 en @{s}=0 → @{a1}=@{L} (y @{a0}=0); con nada en @{s}=1 salen @{a2}=−2·@{L}, @{a3}=@{L}. Por eso lleva el @{L}:
N2 = L*(s - 2*s^2 + s^3)   % giro izquierdo, con su L
%' N3 y N4: lo mismo con descenso y giro unitarios en el extremo DERECHO (@{s}=1):
N3 = 3*s^2 - 2*s^3   % descenso derecho
N4 = L*(-s^2 + s^3)   % giro derecho, con su L
%' Ahora las VEO. Cada Nᵢ es la forma que toma la viga con su dato de nudo = 1. Las de descenso: N1 baja de 1 (izquierda) a 0 (derecha); N3 al revés:
figure
sg = linspace(0,1,150);
plot(sg, 1-3*sg.^2+2*sg.^3, 'b', 'LineWidth', 2); hold on
plot(sg, 3*sg.^2-2*sg.^3, 'r', 'LineWidth', 2)
legend('N1','N3'); xlabel('s'); title('formas de descenso')
figure;
%' Las de giro (dibujo la parte sin el @{L}, que sólo las escala): arrancan de cero, se arquean y vuelven a cero, una a cada lado:
N2f = s - 2*s^2 + s^3
N4f = -s^2 + s^3
figure
sg = linspace(0,1,150);
plot(sg, sg-2*sg.^2+sg.^3, 'b', 'LineWidth', 2); hold on
plot(sg, -sg.^2+sg.^3, 'r', 'LineWidth', 2)
legend('N2f','N4f'); xlabel('s'); title('formas de giro (sin L)')

%" 5 · La curvatura de cada peso (segunda derivada)
%' Derivo cada función dos veces respecto a @{s}. Salen lineales (la curvatura de una cúbica):
N1s = diff(1 - 3*s^2 + 2*s^3, s)   % pendiente de N₁
N1ss = diff(-6*s + 6*s^2, s)   % curvatura de N₁
N2s = diff(L*s - 2*L*s^2 + L*s^3, s)   % pendiente de N₂
N2ss = diff(L - 4*L*s + 3*L*s^2, s)   % curvatura de N₂
N3ss = diff(6*s - 6*s^2, s)   % curvatura de N₃
N4ss = diff(-2*L*s + 3*L*s^2, s)   % curvatura de N₄

%" 6 · El cambio de variable trae las potencias de L
%' Aquí nace el @{EI/L^3}. Como @{s} = @{x/L}, derivar respecto a @{x} mete un @{1/L} por cada derivada: dos derivadas → @{1/L^2}. Y al integrar en @{x} aparece un factor @{L} más. Juntando los tres, el factor común que sale afuera es @{EI/L^3}; adentro quedan las curvaturas en @{s}, con sus factores @{L} de N2 y N4.

%" 7 · La energía: por qué la rigidez es ∫ curvatura·curvatura
%' Ya tengo las curvaturas de las formas. Falta ver por qué la rigidez es su integral. La energía que guarda la viga al doblarse va con la curvatura AL CUADRADO: la energía por unidad de barra es u = ½·@{EI}·κ². Y la deformada es las funciones de forma por los desplazamientos de nudo:
v = N*d   % deformada de la barra
%' Su curvatura son las CURVATURAS de las formas (las @{N''} del paso 5) por esos mismos desplazamientos. Al meter κ² en la energía e integrarla por la barra, cada pareja de nudos deja multiplicando la integral de @{N''} por @{N''}, por @{EI}. Esa es justo la entrada @{K} de la rigidez: cada término es EI·∫ curvatura·curvatura. Es lo que se calcula en el paso 8.

%" 8 · La integral: de dónde salen 12, 6L, 4L²
%' Cada término es la integral del producto de dos curvaturas, de 0 a 1. Los factores @{L} de las funciones de giro quedan dentro y suben la potencia de @{L}:
k11 = int((-6 + 12*s)^2, s, 0, 1)   % descenso izq con descenso izq
k12 = int((-6 + 12*s)*L*(-4 + 6*s), s, 0, 1)   % descenso izq con giro izq
k13 = int((-6 + 12*s)*(6 - 12*s), s, 0, 1)   % descenso izq con descenso der
k22 = int(L*(-4 + 6*s)*L*(-4 + 6*s), s, 0, 1)   % giro izq con giro izq
k24 = int(L*(-4 + 6*s)*L*(-2 + 6*s), s, 0, 1)   % giro izq con giro der
%' Ni un solo factor @{L} en descenso–descenso (queda @{12}), uno en descenso–giro (@{6L}), dos en giro–giro (@{4L^2}). Junto el factor común y la matriz de las 16 integrales:
factorL = EI/L^3   % el factor común
K_coef = [12 6*L -12 6*L; 6*L 4*L^2 -6*L 2*L^2; -12 -6*L 12 -6*L; 6*L 2*L^2 -6*L 4*L^2]   % la matriz de coeficientes
%' La rigidez de la barra es el factor @{factorL} por esa matriz K_coef — toda en símbolos, con @{EI} y @{L}. Cada coeficiente salió de una integral: el @{12} de la de @{(-6+12s)^2}; el @{6L}, la misma con un factor @{L}; el @{4*L^2}, con dos. Ahí queda deducida, término a término, sin meter un solo número.