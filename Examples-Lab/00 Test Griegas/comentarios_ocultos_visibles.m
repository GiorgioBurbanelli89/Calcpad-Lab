% #md
% -> # Comentarios: ocultos vs visibles <-
% -> *Regla: `%` es OCULTO; solo se muestra con un "codigo"* <-
% #endmd

% #md
% ## 1) VISIBLES  (llevan un marcador)
% #endmd

%% Este es un ENCABEZADO (doble %%)

% 'Esta linea lleva apostrofo, se muestra como TEXTO.

% #noc sigma = M*y/I

x = 10       % 'inline con apostrofo, se muestra como caption

% #md
% Y dentro de `% #md`: **negrita**, *cursiva*, `codigo`, tablas y listas.
% #endmd

% #md
% ## 2) OCULTOS  (sin marcador)
% #endmd

% Esta linea con % simple NO aparece (comentario de codigo).
%-- Esta con %-- tampoco aparece.
y = 20       % inline sin apostrofo tampoco aparece

% #hide
z = 99             % se EJECUTA pero no se renderiza nada
disp('esto no se ve en el output')
% #show
w = z + 1          % tras #show vuelve a mostrarse (echo del valor)

% #md
% ## Resumen
% - **Visible:**  `%% titulo`,  `% 'texto`,  `% #noc`,  `% #md`
% - **Oculto:**  `% texto`,  `%-- nota`,  inline `x=5 % nota`,  bloque `% #hide` .. `% #show`
% #endmd
