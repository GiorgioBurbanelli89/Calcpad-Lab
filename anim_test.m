% Prueba de ANIMACION en Calcpad Lab: patch + set + drawnow deben animar en vivo.
X = [0 0; 1 0; 2 0; 0 1; 1 1; 2 1];        % 6 vertices (2 columnas)
F = [1 2 5 4; 2 3 6 5];                     % 2 caras Q4
figure; caxis([0 1]); colormap(jet(256));
ph = patch('Vertices', X, 'Faces', F, 'FaceVertexCData', [0; 0], 'FaceColor', 'flat', 'EdgeColor', 'k');
axis equal;
for k = 1:12
  d = [k/12; 1 - k/12];                     % la cara izquierda sube, la derecha baja
  set(ph, 'FaceVertexCData', d);
  drawnow;                                   % <-- deberia repintar el frame en vivo
  pause(0.15);
end
