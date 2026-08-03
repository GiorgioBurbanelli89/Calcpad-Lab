% #md
% -> # Comentarios: ocultos vs visibles <-
% -> *Todo lo que se puede hacer dentro de `%` en Hekatan Lab* <-
% #endmd

% #md
% ## 1) Comentarios VISIBLES
% #endmd

% Una linea con % simple (en su propia linea) es PROSA visible.

%% Doble %% = ENCABEZADO de seccion

x = 10       % 'con apostrofo: el comentario inline SE MUESTRA como caption

% #noc sigma = M*y/I

% #md
% Dentro de `% #md`: **negrita**, *cursiva*, `codigo`, listas y tablas:
%
% | Marcador | Efecto |
% |----------|--------|
% | `%% t`   | encabezado |
% | `% 'c`   | caption visible |
% | `% #noc` | formula |
% #endmd

% #md
% ## 2) Comentarios OCULTOS
% #endmd

%-- Esta linea con %-- NO aparece en el output (anotacion de codigo).
y = 20       % este comentario inline SIN apostrofo tampoco aparece

% #hide
z = 99            % se EJECUTA pero no se renderiza nada
disp('esto no se ve en el output')
% #show
w = z + 1         % tras #show vuelve a mostrarse (echo del valor)

% #md
% ## Resumen
% - **Visible:**  `% prosa`,  `%% titulo`,  `% 'caption`,  `% #noc`,  `% #md`
% - **Oculto:**  `%-- nota`,  `x=5 % inline`,  bloque `% #hide` … `% #show`
% #endmd
