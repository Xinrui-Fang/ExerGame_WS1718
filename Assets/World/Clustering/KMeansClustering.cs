using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Assets.Utils;
using Assets.World.Clustering;
using UnityEngine;
/* Clustering by K_means methods
 *
 * Sémantique : Le but va être de segmenter la carte afin de délimiter des zones de
 * hauteurs semblables afin de faciliter le positionnement de
 * l'environnement : constructions, routes, arbres ...
 *
 * Parameters : data --> high of points cluster criters
 * 				labels --> association des points aux groupes
 * 				label --> numéro du groupe à clusteriser
 * 				clusterMap --> Matrice qui indique à quel cluster est associé chaque point
 * 				clusterNumber --> nombre de cluster
 *
 * Returns : -> GraphNode : Structure -> Value (Int) //Number du cluster associé
 * 									  -> Location (Vect2I)
 * 									  -> Neightbors (List<GraphNode>)
 * 			 
 */
public class KMeansClustering : ICluster {

	private struct ClusterStruct{
		public List<int> X_Ck ;
		public List<int> Y_Ck ;
		public List<float> Z_Ck;

		public ClusterStruct(List<int> x_Ck, List<int> y_Ck, List<float> z_Ck){
			X_Ck = x_Ck;
			Y_Ck = y_Ck;
			Z_Ck = z_Ck;
		}
	}

	public static GraphNode Cluster(float[,] data, int [,] labels, int label, int clusterNumber, int data_index_1, int data_index_2, float Depth)
	{
		int Rows = labels.GetLength (0);
		int Columns = labels.GetLength (1);

	 	

		// Création d'un rectangle englobant la zone à clusteriser (pour création d'une grille régulière)
		int indI_min = Rows;
		int indI_max = 0;
		int indJ_min = Columns;
		int indJ_max = 0;


		for (int i = 0; i < Rows-1; i++) {
			for (int j = 0; j < Columns-1; j++) {
				if (labels [i, j] == label) {
					if (i < indI_min) {
						indI_min = i;
					}
					if (j < indJ_min) {
						indJ_min = j;
					}
					if (labels [i + 1, j] != label && (i + 1) > indI_max) {
						indI_max = i + 1;
					}
					if (labels [i, j + 1] != label && (j + 1) > indJ_max) {
						indJ_max = j + 1;
					}
				}
			}
		}
		UnityEngine.Debug.Log(String.Format("Data size : {0};{1}", data.GetLength(0), data.GetLength(1)));

		UnityEngine.Debug.Log(String.Format("IndI : {0} / {1}", indI_min, indI_max));
		UnityEngine.Debug.Log(String.Format("IndJ : {0} / {1}", indJ_min, indJ_max));

		//float[,] Rectangle = new float[indI_max - indI_min +1 , indJ_max - indJ_min+1];
		
		int nbl = indI_max - indI_min;
		int nbc = indJ_max - indJ_min;

		int[,] clusterMap = new int[nbl, nbc];

		// Création d'un grille régulière de taille SxS
		int N = nbl * nbc;
		int S = (int) Mathf.Round (Mathf.Sqrt (N / clusterNumber));
		List<int> X_Ck = new List<int>(); // Contient les abscisse des centres
		List<int> Y_Ck = new List<int>(); // Contient les ordonnées des centres
		List<float> Z_Ck = new List<float>(); // Contient les valeurs des centres

		for (int x = (int) Mathf.Round((float) 0.5*S) ; x < nbl ; x += S){
			for (int y = (int) Mathf.Round((float) 0.5*S) ; y < nbc ; y += S){
				X_Ck.Add (x);
				Y_Ck.Add (y);
				Z_Ck.Add (data [x + indI_min, y + indJ_min]);
			}
		}
		int nbCenters = X_Ck.Count;
		
		// Calcul du gradient horizontal de la matrice Rectangle
		float[,] Gx = new float[nbl, nbc];

		for (int i = indI_min ; i < nbl + indI_min ; i++){
			for (int j = indJ_min ; j < nbc + indJ_min; j ++){
				if (j == 0){
					Gx [i- indI_min, j- indJ_min] = data [i, j + 1] - data [i, j];
				}
				else if (j == nbc-1){
					Gx [i- indI_min, j- indJ_min] = data [i, j] - data [i, j - 1];
				}
				else{
					Gx [i- indI_min, j- indJ_min] =(float) 0.5 * (data [i, j + 1] - data [i, j - 1]);
				}
			}
		}

		// Calcul du gradient vertical de la matrice Rectangle
		float[,] Gy = new float[nbl, nbc];

		for (int j = indJ_min ; j < nbc + indJ_min ; j++){
			for (int i = indI_min  ; i < nbl + indI_min; i++){
				
				if (i == 0){
					Gx [i - indI_min, j- indJ_min] = data [1, j] - data [0, j];
				}
				else if (i == nbl-1){
					Gx [i - indI_min, j- indJ_min] = data [i, j] - data [i - 1, j];
				}
				else{
					Gx [i - indI_min, j- indJ_min] = (float) 0.5 * (data [i + 1, j] - data [i - 1, j]);
				}
			}
		}
		

		/* Déplacement des centres
		 * Les centres sont déplacés selon le plus petit
		 * gradient dans un voisinage 3x3
		 */

		for (int i = 0 ; i < nbCenters ; i++){
			float grad_min = float.PositiveInfinity;
			int X_min = 0;
			int Y_min = 0;
			for (int dx = -1 ; dx <= 1 ; dx++){
				for (int dy = -1 ; dy <= 1 ; dy ++){
					int x = Mathf.Min (nbl - 1, Mathf.Max (0, X_Ck[i] + dx));
					int y = Mathf.Min (nbc - 1, Mathf.Max (0, Y_Ck[i] + dy));
					float grad_x = Gx [x, y];
					float grad_y = Gy [x, y];
					if (grad_x+grad_y < grad_min){
						grad_min = grad_x + grad_y;
						X_min = x;
						Y_min = y;
					}
				}
			}
			X_Ck[i] =  X_min;
			Y_Ck[i] = Y_min;
			Z_Ck[i] =  data [X_min, Y_min];
		}
		
		ClusterStruct Ck = new ClusterStruct(X_Ck, Y_Ck, Z_Ck);
		// Initialisation
		float[,] distance_matrix = new float[nbl, nbc];
		clusterMap = new int[nbl, nbc];

		for (int i = 0 ; i < nbl ; i++){
			for (int j = 0 ; j < nbc ; j ++){
				distance_matrix[i, j] = float.PositiveInfinity;
				clusterMap [i, j] = -1;
			}
		}

		float Error = float.PositiveInfinity;
		float seuil = 1;
		int iteration = 0;


		while (Error > seuil && iteration < 20){
			// Pour chaque centre Ck
			UnityEngine.Debug.Log(String.Format("Number of cluster : {0}", Ck.X_Ck.Count));
			UnityEngine.Debug.Log(String.Format("S : {0}", S));
			
			for (int k = 0 ; k <  Ck.X_Ck.Count ; k++){
				// Pour chaque point dans la région 2Sx2S

				for (int i = -2*S ; i <= 2*S ; i++){
					for (int j = -S; j <= S; j++) {
						
						int p_x = (int) Mathf.Max (0, Mathf.Min (nbl-1, Mathf.Round (Ck.X_Ck[k] + i)));
						int p_y = (int) Mathf.Max (0, Mathf.Min (nbc-1, Mathf.Round (Ck.Y_Ck[k] + j)));

						if (labels[p_x + indI_min, p_y + indJ_min] != label){
							continue;
						}
						float p_z = data [p_x + indI_min, p_y + indJ_min];

						int Ck_x = Ck.X_Ck[k];
						int Ck_y = Ck.Y_Ck[k];
						float Ck_z = Ck.Z_Ck[k];

						float D = Distance (p_x, p_y, p_z, Ck_x, Ck_y, Ck_z, Depth);
						
						if (D < distance_matrix [p_x, p_y]) {
							distance_matrix [p_x, p_y] = D;
							clusterMap [p_x, p_y] = k;
						}
						
					}
				}
			}
			ClusterStruct new_Ck = NewCluster(Ck, data, clusterMap, indI_min, indJ_min);
			Error = Erreur_res(Ck, new_Ck);
			UnityEngine.Debug.Log(String.Format("Error {0}", Error));
			Ck = new_Ck;

			iteration ++;

		}
		UnityEngine.Debug.Log(String.Format("Iteration n° {0}", iteration));

		// Printing of the clusterMap
		int[,] img = new int[data.GetLength(0), data.GetLength(1)];

		for (int i = 0 ; i < img.GetLength(0) ; i ++){
			for (int j = 0 ; j < img.GetLength(1) ; j ++){
				img[i, j] = -1;
			}
		}

		for (int i = indI_min ; i < indI_max ; i++){
			for (int j = indJ_min ; j < indJ_max ; j++){
				img[i, j] = clusterMap[i - indI_min, j - indJ_min];
			}
		}

		IntImageExporter CimgExp = new IntImageExporter(-1, Ck.X_Ck.Count);
		CimgExp.Export(string.Format("ClusterMapAt{0}-{1}", data_index_1, data_index_2), img);
		// Construction of the GraphNode
		// Repositionnement de la ClusterMap dans l'espace

		//return CreateGraphNodeAt(0, 0, clusterMap, indI_min, indJ_min,  new LinkedList<Couple>());

	return new GraphNode();
	}

	/* Fonction
	 * Nom : Distance
	 * Sémantique : Calcul la distance entre un point et le centre du cluster qui lui est associé
	 * Paramètres : p_z : Float : Hauteur du point à considérer
	 * 				Ck_z : Float : Hauteur du centre du cluster
	 * Retour : Float : Différence des hauteurs
	 */
	private static float Distance(int p_x, int p_y, float p_z, int Ck_x, int Ck_y, float Ck_z, float Depth){
		return Mathf.Abs(p_z - Ck_z)*Mathf.Sqrt((p_x-Ck_x)*(p_x-Ck_x) + (p_y - Ck_y)*(p_y - Ck_y) + Depth*Depth*(p_z - Ck_z) * (p_z - Ck_z));
	}

	/* Fonction
	 * Nom : NewCluster
	 * Sémantique : Replace le centre du cluster à la moyenne de ses points
	 * Paramètres ; X_Ck : Int[] abscisse des centres
	 * 				Y_Ck : Int[] ordonnée des centres
	 * 				Z_Ck : Float[] hauteur des centres
	 * 				data : Float[ , ] liste des hauteurs de la map
	 * 				clusterMap : Int[ , ] matrice des numéros de cluster associés aux points
	 * Retour : new_X_Ck, new_Y_Ck, new_Z_Ck, mêmes types que les paramètres
	 */
	private static ClusterStruct NewCluster(ClusterStruct Ck_data, float[,] data, int[,] clusterMap, int indI_min, int indJ_min){
		ClusterStruct Ck = new ClusterStruct();
		Ck.X_Ck= new List<int>(Ck_data.X_Ck);
		Ck.Y_Ck= new List<int>(Ck_data.Y_Ck);
		Ck.Z_Ck =  new List<float> (Ck_data.Z_Ck);
		for (int k = 0 ; k < Ck.X_Ck.Count ; k++){
			List<int> Indices_x = new List<int>();
			List<int> Indices_y = new List<int>();
			for (int i = 0 ; i < clusterMap.GetLength(0) ; i++){
				for (int j = 0 ; j < clusterMap.GetLength(1) ; j++){
					if (clusterMap[i, j] == k){
						Indices_x.Add(i);
						Indices_y.Add(j);
					}
				}
			}
			if(Indices_x.Count != 0){
				int x_Ck = 0;
				int y_Ck = 0;
				float z_Ck = 0; 
				for (int ind = 0 ; ind < Indices_x.Count ; ind ++){
					x_Ck = x_Ck + Indices_x[ind];
					y_Ck = y_Ck + Indices_y[ind];
					z_Ck = z_Ck + data[Indices_x[ind] + indI_min, Indices_y[ind] + indJ_min];
				} 
				x_Ck = (int) Mathf.Round(x_Ck/Indices_x.Count);
				y_Ck = (int) Mathf.Round(y_Ck/Indices_y.Count);
				z_Ck = z_Ck/Indices_x.Count;
				Ck.X_Ck[k] = x_Ck;
				Ck.Y_Ck[k] = y_Ck;
				Ck.Z_Ck[k] = z_Ck;
			}else{continue;}
		}
		ClusterStruct new_Ck = new ClusterStruct(Ck.X_Ck, Ck.Y_Ck, Ck.Z_Ck);
		return new_Ck;
	}

	private static float Erreur_res(ClusterStruct Ck, ClusterStruct new_Ck){
		float E = 0;
		for(int ind = 0 ; ind < Ck.X_Ck.Count; ind ++){
			E = E + Mathf.Abs(Ck.X_Ck[ind]-new_Ck.X_Ck[ind]) + Mathf.Abs(Ck.Y_Ck[ind]-new_Ck.Y_Ck[ind]);
		}
		return E;
	}

	private static GraphNode CreateGraphNodeAt(int x, int y, int[,] clusterMap, int indI_min, int indJ_min, LinkedList<Couple> list_positions){
		// For loop --> comme au début
		int Value = clusterMap[x, y];
		Vector2Int Position = new Vector2Int(x+indI_min, y+indJ_min);
		GraphNode G = new GraphNode(Value, Position);

		list_positions.AddLast(new Couple(x, y));

		int ind_mx = Mathf.Max(0, x-1);
		int ind_Mx = Mathf.Min(clusterMap.GetLength(0)-1, x+1);
		int ind_my = Mathf.Max(0, y-1);
		int ind_My = Mathf.Min(clusterMap.GetLength(1)-1, y+1);

		for (int i = ind_mx ; i <= ind_Mx ; i++){
			for (int j = ind_my ; j <= ind_My ; j++){
				if (list_positions.Find(new Couple(i, j))== null){
					G.addNeighbours(CreateGraphNodeAt(i, j, clusterMap, indI_min, indJ_min, list_positions));
				}
			}
		}
		return G;
	}

	private struct Couple{
		int X;
		int Y;

		public Couple(int x, int y){
			X = x;
			Y = y;
		}
	}
}
