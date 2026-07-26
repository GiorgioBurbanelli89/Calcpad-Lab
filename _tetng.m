[Xg,Yg,Zg]=ndgrid(0:0.34:1, 0:0.34:1, 0:0.34:1);
X=[Xg(:) Yg(:) Zg(:)];
disp('nodos:'); disp(size(X,1));
disp('ok ndgrid 3D en Lab');
