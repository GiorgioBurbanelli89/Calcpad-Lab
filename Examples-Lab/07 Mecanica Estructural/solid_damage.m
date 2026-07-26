% =====================================================================================
%  SOLID DAMAGE - Cilindro de concreto a compresion (dano 3D real, OpenSees=STKO)
% -------------------------------------------------------------------------------------
%  Renderiza la piel del cilindro deformada + coloreada por el DANO d(0..1) del
%  material ASDConcrete3D (Lubliner + IMPL-EX), corrido en OpenSees (= el solver de
%  STKO). Los datos (coords/superficie/desplazamiento/dano por paso) vienen de esa
%  corrida real -> IDENTICO a STKO porque son los MISMOS datos del solver.
%  Anima paso a paso (patch/trisurf) con CAMARA CANONICA view(35,12) para poder
%  validar frame a frame y motor a motor (Lab == Octave == MATLAB, mismo pixel).
%  Fisica: extremos confinados (placas) NO fallan; el cortante LOCALIZA en una banda
%  a media altura -> banda de dano roja (jet_r), extremos azules. N,mm.
% =====================================================================================
coords = csvread('cyl_damage_data/coords.csv');      % N x 3 nodos
surf   = csvread('cyl_damage_data/surf.csv');        % caras de la piel (quads, 1-based)
Dn     = csvread('cyl_damage_data/damage_node.csv'); % nst x N  dano nodal por paso
U      = csvread('cyl_damage_data/U.csv');           % (nst*N) x 3  desplazamiento por paso
meta   = csvread('cyl_damage_data/meta.csv');        % [nst N]
nst = meta(1); N = meta(2); sc = 15;                 % factor de escala de la deformada

% limites fijos (deformada final) para que la camara no salte entre frames
Vend = coords + sc*U((nst-1)*N+1:nst*N,:);
mn = [min(Vend(:,1)) min(Vend(:,2)) min(Vend(:,3))] - 8;   % por columna (Lab min(M)=escalar)
mx = [max(Vend(:,1)) max(Vend(:,2)) max(Vend(:,3))] + 8;
lims = [mn(1) mx(1) mn(2) mx(2) mn(3) mx(3)];

figure('Position',[100 100 700 600]); colormap(jet_r);
for st = 1:nst                                       % --- ANIMA (frame a frame) ---
  Uf = U((st-1)*N+1:st*N,:); V = coords + sc*Uf; dn = Dn(st,:)';
  cla;
  patch('Faces',surf,'Vertices',V,'FaceVertexCData',dn, ...
        'FaceColor','interp','EdgeColor','none');
  axis equal; axis(lims); view(35,12); caxis([0 1]); colorbar; rotate3d on;
  xlabel('x'); ylabel('y'); zlabel('z');
  title(sprintf('Solid Damage - cilindro ASDConcrete3D (OpenSees=STKO) - paso %d/%d  d_{max}=%.3f', ...
        st, nst, max(dn)));
  drawnow;
end
fprintf('FIN: dano max = %.3f (banda de cortante localizada a media altura; datos OpenSees=STKO)\n', max(Dn(nst,:)));
