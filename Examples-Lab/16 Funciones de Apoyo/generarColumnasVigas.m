function [b, h] = generarColumnasVigas(nudcol, nudvg, baseColumnas, alturaColumnas, baseVigas, alturaVigas)
    % Programa para generar la base y altura de las columnas y vigas en una sola columna
    % con nudt filas
    %-------------------------------------------------------------%
    % Por: [Tu Nombre]
    % CEINCI-ESPE
    %-------------------------------------------------------------%
    % [B, H] = generarColumnasVigas(nudcol, nudvg, baseColumnas, alturaColumnas, baseVigas, alturaVigas)
    %-------------------------------------------------------------%
    % nudcol: Número de columnas
    % nudvg: Número de vigas
    % baseColumnas: Base de las columnas
    % alturaColumnas: Altura de las columnas
    % baseVigas: Base de las vigas
    % alturaVigas: Altura de las vigas
    % B: Vector de bases con tamaño [nudt x 1]
    % H: Vector de alturas con tamaño [nudt x 1]
    
    % Calcular el número total de elementos
    nudt = nudcol + nudvg;
    
    % Inicializar los vectores de bases y alturas
    b = zeros(nudt, 1);
    h = zeros(nudt, 1);
    
    % Asignar los valores de las bases y alturas para las columnas
    b(1:nudcol) = baseColumnas;
    h(1:nudcol) = alturaColumnas;
    
    % Asignar los valores de las bases y alturas para las vigas
    b(nudcol+1:nudt) = baseVigas;
    h(nudcol+1:nudt) = alturaVigas;
end
