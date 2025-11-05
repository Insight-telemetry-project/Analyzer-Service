using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class GrangerCausalityAnalyzer : IGrangerCausalityAnalyzer
    {
        public double ComputeCausality(List<double> xSeries, List<double> ySeries, int lagCount)
        {

            int totalSamples = xSeries.Count;
            List<double[]> yLagMatrix = BuildLagMatrix(ySeries, lagCount);
            List<double[]> xyCombinedLagMatrix = BuildCombinedLagMatrix(xSeries, ySeries, lagCount);
            List<double> targetYValues = ySeries.Skip(lagCount).ToList();

            double meanSquaredErrorY = ComputeMeanSquaredError(yLagMatrix, targetYValues);
            double meanSquaredErrorXY = ComputeMeanSquaredError(xyCombinedLagMatrix, targetYValues);

            double improvementRatio = (meanSquaredErrorY - meanSquaredErrorXY) / meanSquaredErrorY;

            return improvementRatio;
        }

        private List<double[]> BuildLagMatrix(List<double> series, int lagCount)
        {
            List<double[]> lagMatrix = new List<double[]>();

            for (int currentIndex = lagCount; currentIndex < series.Count; currentIndex++)
            {
                double[] lagWindow = new double[lagCount];
                for (int lagIndex = 0; lagIndex < lagCount; lagIndex++)
                {
                    lagWindow[lagIndex] = series[currentIndex - lagIndex - 1];
                }
                lagMatrix.Add(lagWindow);
            }

            return lagMatrix;
        }

        private List<double[]> BuildCombinedLagMatrix(List<double> xSeries, List<double> ySeries, int lagCount)
        {
            List<double[]> combinedMatrix = new List<double[]>();

            for (int currentIndex = lagCount; currentIndex < xSeries.Count; currentIndex++)
            {
                double[] lagWindow = new double[lagCount * 2];

                for (int lagIndex = 0; lagIndex < lagCount; lagIndex++)
                {
                    lagWindow[lagIndex] = ySeries[currentIndex - lagIndex - 1];
                    lagWindow[lagIndex + lagCount] = xSeries[currentIndex - lagIndex - 1];
                }

                combinedMatrix.Add(lagWindow);
            }

            return combinedMatrix;
        }

        private double ComputeMeanSquaredError(List<double[]> inputSamples, List<double> targetValues)
        {
            int totalSamples = inputSamples.Count;
            int totalFeatures = inputSamples[0].Length;

            double[,] inputMatrix = new double[totalSamples, totalFeatures + 1];
            double[] targetArray = targetValues.ToArray();

            for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
            {
                for (int featureIndex = 0; featureIndex < totalFeatures; featureIndex++)
                {
                    inputMatrix[sampleIndex, featureIndex] = inputSamples[sampleIndex][featureIndex];
                }
                inputMatrix[sampleIndex, totalFeatures] = 1.0;
            }

            Matrix<double> matrixX = Matrix<double>.Build.DenseOfArray(inputMatrix);
            Vector<double> vectorY = Vector<double>.Build.Dense(targetArray);

            Vector<double> coefficients = (matrixX.TransposeThisAndMultiply(matrixX))
                .Inverse()
                .Multiply(matrixX.TransposeThisAndMultiply(vectorY));

            Vector<double> predictions = matrixX.Multiply(coefficients);
            double meanSquaredError = predictions.Subtract(vectorY).PointwisePower(2).Average();

            return meanSquaredError;
        }
    }
}
