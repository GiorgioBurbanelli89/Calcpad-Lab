# =====================================================================================
#  CILINDRO DE CONCRETO A COMPRESION con nDMaterial ASDConcrete3D (Petracca/Camata, ASDEA)
#  Solver = OpenSees (= motor de STKO). Malla estructurada de bricks mapeada del disco.
#  Graba el CAMPO DE DANO por elemento (combinado 1-(1-dt)*(1-dc)) en cada paso y exporta CSV.
#  Unidades: N, mm, MPa.  Parametros del strain-loc de STKO (ver cilindro_asdconcrete.m).
# =====================================================================================
wipe
model basic -ndm 3 -ndf 3

set OUT "C:/Users/j-b-j/Documents/Hekatan Calc 1.0.0/Calcpad-Lab/_sueltos/cyl_damage"

# ---------------- geometria ----------------
set R 75.0; set Lz 300.0
set nx 8; set ny 8; set nz 16
set E 27606.0; set nu 0.2
set umax 6.0; set nstep 30

# ---------------- curvas ASDConcrete3D (strain-loc STKO) ----------------
# Te/Ts = tension (strain/stress);  Td = dano de tension (0..0.99) alineado con Te
set Te {0 1.011e-4 1.684e-4 5.693e-3 2.813e-2 2.813e-1}
set Ts {0 2.79 3.1 0.62 0.0031 0.0031}
set Td {0 0 0.30 0.95 0.99 0.99}
# Ce/Cs = compresion;  Cd = dano de compresion (0..0.90) alineado con Ce
set Ce {0 5e-4 6.666e-4 8.332e-4 9.999e-4 1.167e-3 1.333e-3 1.5e-3 1.667e-3 1.833e-3 2e-3 0.3233 0.3238}
set Cs {0 13.8 18.15 21.98 25.30 28.11 30.41 32.20 33.48 34.24 34.5 6.9 6.9}
set Cd {0 0 0.01 0.04 0.08 0.12 0.17 0.22 0.27 0.32 0.37 0.90 0.90}

# longitud caracteristica ~ tamano de elemento (radial y axial ~ 18.75 mm)
set lch [expr {$Lz/$nz}]
nDMaterial ASDConcrete3D 1 $E $nu \
    -Te {*}$Te -Ts {*}$Ts -Td {*}$Td \
    -Ce {*}$Ce -Cs {*}$Cs -Cd {*}$Cd \
    -rho 0.0 -eta 0.0 -Kc 0.6667 -cdf 0.0 \
    -implex -implexAlpha 1.0 -autoRegularization $lch

# ---------------- malla mapeada del disco ----------------
proc nid {i j k nx ny} { return [expr {$k*($nx+1)*($ny+1)+$j*($nx+1)+$i+1}] }
set N [expr {($nx+1)*($ny+1)*($nz+1)}]
expr {srand(7)}
for {set k 0} {$k<=$nz} {incr k} {
  for {set j 0} {$j<=$ny} {incr j} {
    for {set i 0} {$i<=$nx} {incr i} {
      set u [expr {-1.0+2.0*$i/$nx}]; set v [expr {-1.0+2.0*$j/$ny}]
      set x [expr {$R*$u*sqrt(max(0.0,1.0-$v*$v/2.0))}]
      set y [expr {$R*$v*sqrt(max(0.0,1.0-$u*$u/2.0))}]
      set z [expr {$Lz*$k/$nz}]
      # imperfeccion geometrica pequena en el interior (rompe simetria -> banda localiza natural)
      if {$k>0 && $k<$nz && $i>0 && $i<$nx && $j>0 && $j<$ny} {
        set x [expr {$x+0.3*(rand()-0.5)}]; set y [expr {$y+0.3*(rand()-0.5)}]
        set z [expr {$z+0.3*(rand()-0.5)}]
      }
      set id [nid $i $j $k $nx $ny]
      node $id $x $y $z
      set ::CX($id) $x; set ::CY($id) $y; set ::CZ($id) $z
    }
  }
}

# ---------------- elementos stdBrick (8 GP) ----------------
set NE [expr {$nx*$ny*$nz}]
set e 0
for {set k 0} {$k<$nz} {incr k} {
  for {set j 0} {$j<$ny} {incr j} {
    for {set i 0} {$i<$nx} {incr i} {
      incr e
      set n1 [nid $i $j $k $nx $ny];         set n2 [nid [expr {$i+1}] $j $k $nx $ny]
      set n3 [nid [expr {$i+1}] [expr {$j+1}] $k $nx $ny]; set n4 [nid $i [expr {$j+1}] $k $nx $ny]
      set n5 [nid $i $j [expr {$k+1}] $nx $ny];         set n6 [nid [expr {$i+1}] $j [expr {$k+1}] $nx $ny]
      set n7 [nid [expr {$i+1}] [expr {$j+1}] [expr {$k+1}] $nx $ny]; set n8 [nid $i [expr {$j+1}] [expr {$k+1}] $nx $ny]
      element stdBrick $e $n1 $n2 $n3 $n4 $n5 $n6 $n7 $n8 1
      set ::EI($e) $i; set ::EJ($e) $j; set ::EK($e) $k
      set ::EL($e) [list $n1 $n2 $n3 $n4 $n5 $n6 $n7 $n8]
      foreach nn [list $n1 $n2 $n3 $n4 $n5 $n6 $n7 $n8] { lappend ::NADJ($nn) $e }  ;# nodo -> elementos que lo tocan
    }
  }
}

# ---------------- BC: base empotrada, tope confinado + placa rigida en uz ----------------
set master [nid [expr {$nx/2}] [expr {$ny/2}] $nz $nx $ny]
set topnodes {}
for {set i 0} {$i<=$nx} {incr i} {
  for {set j 0} {$j<=$ny} {incr j} {
    set nb [nid $i $j 0 $nx $ny];  fix $nb 1 1 1
    set nt [nid $i $j $nz $nx $ny]; fix $nt 1 1 0
    lappend topnodes $nt
  }
}
foreach nt $topnodes { if {$nt != $master} { equalDOF $master $nt 3 } }

# carga de referencia (compresion) sobre el master; DisplacementControl impone uz
timeSeries Linear 1
pattern Plain 1 1 { load $master 0.0 0.0 -1.0 }

constraints Transformation
numberer RCM
system BandGeneral
test NormDispIncr 1.0e-5 60 0
algorithm KrylovNewton
analysis Static

# ---------------- caras de la superficie exterior (para dibujar la piel) ----------------
# fmap local (1-based dentro del brick), dirn = direccion de la cara frontera
set fmap {{1 2 3 4} {5 6 7 8} {1 2 6 5} {2 3 7 6} {3 4 8 7} {4 1 5 8}}
set dirn {{0 0 -1} {0 0 1} {0 -1 0} {1 0 0} {0 1 0} {-1 0 0}}
set fS [open "$OUT/surf.csv" w]
set faceElems {}   ;# elemento dueño de cada cara, en el MISMO orden que las filas de surf.csv
for {set ee 1} {$ee<=$NE} {incr ee} {
  set i $::EI($ee); set j $::EJ($ee); set k $::EK($ee); set el $::EL($ee)
  for {set f 0} {$f<6} {incr f} {
    set d [lindex $dirn $f]
    set ni [expr {$i+[lindex $d 0]}]; set nj [expr {$j+[lindex $d 1]}]; set nk [expr {$k+[lindex $d 2]}]
    if {$ni<0||$ni>=$nx||$nj<0||$nj>=$ny||$nk<0||$nk>=$nz} {
      set fc [lindex $fmap $f]
      set a [lindex $el [expr {[lindex $fc 0]-1}]]; set b [lindex $el [expr {[lindex $fc 1]-1}]]
      set c [lindex $el [expr {[lindex $fc 2]-1}]]; set dd [lindex $el [expr {[lindex $fc 3]-1}]]
      puts $fS "$a,$b,$c,$dd"
      lappend faceElems $ee
    }
  }
}
close $fS
set NF [llength $faceElems]

# coordenadas nodales (una fila por nodo, orden 1..N)
set fC [open "$OUT/coords.csv" w]
for {set id 1} {$id<=$N} {incr id} { puts $fC "$::CX($id),$::CY($id),$::CZ($id)" }
close $fC

# ---------------- solve con substep adaptativo + cascada de algoritmos ----------------
proc trystep {du} {
  integrator DisplacementControl $::master 3 $du
  foreach alg {KrylovNewton NewtonLineSearch Broyden} {
    algorithm $alg
    if {[analyze 1]==0} { algorithm KrylovNewton; return 1 }
  }
  algorithm KrylovNewton; return 0
}
proc advance {du lvl} {
  if {[trystep $du]} { return 1 }
  if {$lvl>=7} { return 0 }
  set h [expr {$du/2.0}]
  if {![advance $h [expr {$lvl+1}]]} { return 0 }
  return [advance $h [expr {$lvl+1}]]
}

set fU [open "$OUT/U.csv" w]
set fD [open "$OUT/damage.csv" w]
set fFace [open "$OUT/damage_face.csv" w]   ;# nst x NF : dano del elemento dueño de cada cara de surf.csv
set fNode [open "$OUT/damage_node.csv" w]   ;# nst x N  : dano interpolado a nodos (promedio de elementos vecinos)
set du [expr {-$umax/$nstep}]
set done 0
puts "paso  acort(mm)  dano_max  #d>0.5"
for {set s 1} {$s<=$nstep} {incr s} {
  if {![advance $du 0]} { puts "  ** no convergio en el paso $s (punto limite / snap); detengo aqui" ; break }
  incr done
  # --- U por nodo ---
  for {set id 1} {$id<=$N} {incr id} {
    set d [nodeDisp $id]
    puts $fU "[lindex $d 0],[lindex $d 1],[lindex $d 2]"
  }
  # --- dano por elemento: promedio 8 GP del combinado 1-(1-dt)*(1-dc) ---
  set dmax 0.0; set ncrk 0
  for {set ee 1} {$ee<=$NE} {incr ee} {
    set acc 0.0
    for {set gp 1} {$gp<=8} {incr gp} {
      set r [eleResponse $ee material $gp damage]
      set dt [lindex $r 0]; set dc [lindex $r 1]
      if {$dt==""} { set dt 0.0 }; if {$dc==""} { set dc 0.0 }
      set acc [expr {$acc + (1.0-(1.0-$dt)*(1.0-$dc))}]
    }
    set dcomb [expr {$acc/8.0}]
    set Dele($ee) $dcomb
    puts $fD $dcomb
    if {$dcomb>$dmax} { set dmax $dcomb }
    if {$dcomb>0.5} { incr ncrk }
  }
  # --- fila de damage_face.csv: dano del elemento dueño, en orden de surf.csv ---
  set frow {}
  foreach fe $faceElems { lappend frow $Dele($fe) }
  puts $fFace [join $frow ","]
  # --- fila de damage_node.csv: promedio de los elementos que tocan cada nodo ---
  set nrow {}
  for {set id 1} {$id<=$N} {incr id} {
    set sum 0.0; set cnt 0
    if {[info exists ::NADJ($id)]} {
      foreach el $::NADJ($id) { set sum [expr {$sum+$Dele($el)}]; incr cnt }
    }
    lappend nrow [expr {$cnt>0 ? $sum/$cnt : 0.0}]
  }
  puts $fNode [join $nrow ","]
  puts [format "%3d    %6.3f    %6.3f     %d" $s [expr {-$s*$du}] $dmax $ncrk]
}
close $fU
close $fD
close $fFace
close $fNode

# meta: nst (pasos grabados), N (nodos)
set fM [open "$OUT/meta.csv" w]
puts $fM "nst,N"
puts $fM "$done,$N"
close $fM

puts "LISTO: pasos grabados=$done  nodos=$N  elementos=$NE  caras=$NF"
wipe
