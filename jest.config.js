/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  roots: ['<rootDir>/server/src/node/test'],
  transform: {
    '^.+\\.ts$': ['ts-jest', {
      tsconfig: 'tsconfig.node.json'
    }]
  }
};
