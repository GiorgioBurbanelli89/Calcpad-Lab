%' # La formulacion del DECK — que es y como entrega la carga
%'
%' Todo lo que sigue esta MEDIDO contra el ETABS instalado, no deducido de la
%' documentacion. Donde hay un numero, salio de correr el programa.

%' ## 1. Un deck NO tiene rigidez a flexion
%'
%' Se le pidio a ETABS un deck con las tres clases de cascara posibles:
%'
%'     SetDeck(nombre, DeckType=1, ShellType=1, ...)  ->  devolvio 0 (OK)
%'     GetDeck(nombre)                                ->  ShellType = 3
%'
%'     pedido ShellType=1  ->  quedo 3
%'     pedido ShellType=2  ->  quedo 3
%'     pedido ShellType=3  ->  quedo 3
%'
%' El 3 es MEMBRANA. ETABS ignora lo que se le pida: un deck es SIEMPRE
%' membrana. Y se comprobo por comportamiento: un panel de deck apoyado en dos
%' bordes y cargado NO flecta, mientras la misma losa maciza de 12 cm da
%' -9.622 mm. No es que tenga poca rigidez cruzada — no tiene ninguna.

%' ## 2. Entonces, .que aporta?
%'
%' Rigidez EN SU PLANO, y nada fuera de el. La matriz constitutiva de membrana:

% #noc D_m = E·t/(1-ν²) · [ 1 ν 0 ; ν 1 0 ; 0 0 (1-ν)/2 ]

%' y la de flexion, que en un deck NO se ensambla:

% #noc D_b = E·t³/(12·(1-ν²)) · [ 1 ν 0 ; ν 1 0 ; 0 0 (1-ν)/2 ]

%' El t³ es la clave de por que el zinc envenena el calculo: con t = 0.8 mm
%' contra los 250 mm de una viga, la flexion difiere en DOCE ordenes de
%' magnitud dentro de la misma matriz. Se comprobo: tres binarios distintos
%' daban -83, -2394 y -1e17 mm del MISMO modelo. Eso no es un solver malo, es
%' una matriz mal condicionada.

%' ## 3. Como llega la carga a las vigas
%'
%' Una carga de superficie entra al FEM por un unico camino: su vector de
%' fuerzas nodales equivalente. No hay otro.

% #noc f_i = ∫∫ N_i · q · dA

%' Se integra en el cuadrado patron con el jacobiano real. Lo deduzco simbolico
%' para un Q4, que es lo que usa el deck.

syms xi eta a b q real
N = [ (1-xi)*(1-eta); (1+xi)*(1-eta); (1+xi)*(1+eta); (1-xi)*(1+eta) ] / 4;

%' Para una celda RECTANGULAR de lados a x b el jacobiano es constante,
%' detJ = a*b/4, y la integral sale exacta:
detJ = a*b/4;
f = simplify(int(int(N*q*detJ, xi, -1, 1), eta, -1, 1));
disp('vector de carga consistente de un Q4 rectangular:')
disp(f)

%' Da q*a*b/4 en cada nudo, o sea la CUARTA PARTE del area por q. Se comprueba:
disp('.coincide con q*A/4 en los cuatro? (debe dar 0 0 0 0):')
disp(simplify(f.' - q*a*b/4))

%' Eso confirma lo medido en ETABS: un panel rectangular de 4x3 m con
%' q = 10 kN/m2 dio 30.000 kN en cada uno de sus cuatro nudos, y la suma
%' 120.000 kN exacta. En un TRAPECIO, en cambio, el jacobiano ya no es
%' constante y los cuatro nudos reciben distinto — ahi se separa el reparto
%' consistente del ingenuo "area sobre cuatro".

%' ## 4. Por que el TAMANO de celda cambia el resultado
%'
%' Aca esta lo que costo encontrar. Si la celda abarca el vano entero entre
%' vigas principales, sus cuatro esquinas caen sobre esas principales y toda
%' la carga se les entrega directo: las VIGUETAS del medio no reciben nada.

L_vano = 5.025;     % m, entre vigas principales
sep_vig = 1.024;    % m, separacion de viguetas
q_deck = 9.30;      % kN/m2

%' Celda gruesa: un solo Q4 cubriendo el vano
A_gruesa = L_vano * sep_vig;
F_esquina_gruesa = q_deck * A_gruesa / 4

%' Celda fina (~1 m), que es lo que malla ETABS
NX = 5;
A_fina = (L_vano/NX) * sep_vig;
F_esquina_fina = q_deck * A_fina / 4

%' Con la celda fina la carga se reparte a lo largo de la vigueta en vez de
%' concentrarse en sus extremos. Medido contra ETABS en el mezanine:
%'
%'     celdas 5.025 x 1.024   ->  diferencia media 10.40 %   maxima 15.12 %
%'     celdas ~1.0  x 1.024   ->  diferencia media  0.53 %   maxima  2.08 %
%'
%' Misma carga total, mismo solver, misma geometria. Lo unico que cambio fue
%' donde entra la carga.

%' ## 5. La direccion la manda el EJE LOCAL, no la geometria
%'
%' En el binario de ETABS estan los dos tokens que lo gobiernan:
%'
%'     ONEWAYLOADDIST          la seccion reparte en UN sentido
%'     SLABRIBPARALLELTOAXIS   a que eje local van paralelos los nervios
%'
%' El deck salva PERPENDICULAR a las vigas secundarias y les entrega la carga.
%' En el galpon las viguetas corren en X, asi que el deck salva en Y y el eje
%' local 1 va a 90 grados. Verificado leyendolo del modelo: las 10 areas
%' devolvieron 90.0.

%' ## 6. Y sobre que area se integra
%'
%' El parametro `Dir` de la carga decide si se integra sobre el area REAL de la
%' superficie o sobre su PROYECCION. Medido con un faldon al 15 % de pendiente:

pend = 0.15;
A_proy = 6*4;
A_real = A_proy * sqrt(1 + pend^2);
relacion = A_real / A_proy

%'     Dir 6  = Z global      ->  area REAL          242.685 kN
%'     Dir 9  = Z proyectado  ->  area PROYECTADA    240.000 kN
%'     Dir 10 = gravedad      ->  area REAL         -242.685 kN
%'     Dir 11 = grav. proyect.->  area PROYECTADA   -240.000 kN
%'
%' El zinc pesa por m2 de faldon: va sobre area REAL. La sobrecarga de cubierta
%' la norma la da por m2 de planta: va PROYECTADA.

%' ## Resumen de la regla
%'
%' - deck y zinc: MEMBRANA, sin flexion. No rigidizan como losa.
%' - su papel es entregar la carga, en un sentido, a las secundarias.
%' - la malla tiene que ser FINA donde el elemento puede sostener sus nudos, y
%'   solo alineada con la estructura donde no (el zinc de 0.8 mm).
%' - el eje local decide a quien le entrega.
