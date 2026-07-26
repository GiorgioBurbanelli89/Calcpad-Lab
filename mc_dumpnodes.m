kref=1; frame_fiber;
UX=Ug(1:6:end); UY=Ug(2:6:end); UZ=Ug(3:6:end);
M=[(1:NN)' UX UY UZ];
dlmwrite('frame_matlab_nodes.csv',M,'precision','%.8e');
disp('nodos MATLAB guardados');
