using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.ML;
using Emgu.CV.ML.MlEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SudokuSolver.Helpers
{
    public class DigitRecogniser
    {
        private const string TrainingDataPath = "train.csv";

        private readonly Matrix<float> trainingData;
        private readonly Matrix<int> trainingLabels;

        private readonly SVM svm;

        public DigitRecogniser()
        {
            var trainingData = new List<float[]>();
            var trainingLabels = new List<int>();

            var reader = new StreamReader(TrainingDataPath);

            string line = string.Empty;

            while ((line = reader.ReadLine()) != null)
            {
                int firstIndex = line.IndexOf(",");
                int currentLabel = Convert.ToInt32(line.Substring(0, firstIndex));
                string currentData = line.Substring(firstIndex + 1);
                float[] data = currentData.Split(',').Select(x => float.Parse(x)).ToArray();

                trainingData.Add(data);
                trainingLabels.Add(currentLabel);
            }

            this.trainingData = new Matrix<float>(To2D(trainingData.ToArray()));
            this.trainingLabels = new Matrix<int>(trainingLabels.ToArray());

            if (File.Exists("svm.txt"))
            {
                svm = new SVM();
                FileStorage file = new FileStorage("svm.txt", FileStorage.Mode.Read);
                svm.Read(file.GetNode("opencv_ml_svm"));
            }
            else
            {
                svm = new SVM
                {
                    C = 100,
                    Type = SVM.SvmType.CSvc,
                    Gamma = 0.005,
                    TermCriteria = new MCvTermCriteria(1000, 1e-6)
                };

                svm.SetKernel(SVM.SvmKernelType.Linear);
                svm.Train(this.trainingData, DataLayoutType.RowSample, this.trainingLabels);
                svm.Save("svm.txt");
            }
        }

        private T[,] To2D<T>(T[][] source)
        {
            try
            {
                int FirstDim = source.Length;
                int SecondDim = source.GroupBy(row => row.Length).Single().Key;

                var result = new T[FirstDim, SecondDim];
                for (int i = 0; i < FirstDim; ++i)
                    for (int j = 0; j < SecondDim; ++j)
                        result[i, j] = source[i][j];

                return result;
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("The given jagged array is not rectangular.");
            }
        }

        public int Classify(Mat currentCell)
        {
            Mat cloneImage = currentCell.Clone();
            cloneImage.ConvertTo(cloneImage, DepthType.Cv32F);
            CvInvoke.Resize(cloneImage, cloneImage, new Size(trainingData.Size.Width, 1));

            var xx = new Matrix<byte>(cloneImage.Rows, cloneImage.Cols);
            cloneImage.CopyTo(xx);

            return (int)svm.Predict(xx);
        }
    }
}
