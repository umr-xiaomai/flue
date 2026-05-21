<template>
  <div class="flex flex-col items-center justify-center min-h-screen bg-gray-100 p-4">
    <h1 class="text-3xl font-bold text-blue-500 mb-4">Flue App</h1>
    <p class="text-base text-gray-600 mb-6">Built with Vue 3 + Tailwind, running on Flutter</p>

    <div class="flex flex-row gap-4 mb-6">
      <router-link to="/" class="text-blue-500 font-semibold">Home</router-link>
      <router-link to="/about" class="text-blue-500 font-semibold">About</router-link>
    </div>

    <div class="flex flex-col items-center gap-3 w-full max-w-md">
      <input
        v-model="name"
        placeholder="Enter your name"
        class="border border-gray-300 rounded-lg px-4 py-2 w-full"
      />
      <p v-if="name" class="text-lg text-green-600">Hello, {{ name }}!</p>

      <button @click="increment" class="bg-blue-500 text-white px-6 py-3 rounded-xl font-semibold">
        Count: {{ count }}
      </button>

      <ul class="w-full">
        <li v-for="item in items" class="text-sm text-gray-700 py-1">{{ item }}</li>
      </ul>

      <hr class="w-full" />

      <div class="flex flex-row gap-2">
        <img src="https://picsum.photos/100" class="rounded-full size-10" />
        <p class="text-sm text-gray-500">Status: {{ statusText }}</p>
      </div>
    </div>
  </div>
</template>

<script lang="ts">
const count = ref(0);
const name = ref("");
const items = ["Learn Flue", "Build UI", "Deploy to Flutter"];
const statusText = ref("Ready");

function increment() {
  count.value++;
}

onMounted(() => {
  console.log("App mounted");
});
</script>
